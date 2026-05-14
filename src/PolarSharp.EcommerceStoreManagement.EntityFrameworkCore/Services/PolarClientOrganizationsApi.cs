using Microsoft.Extensions.Logging;
using Microsoft.Kiota.Abstractions;
using PolarSharp.EcommerceStoreManagement.Services;
using PolarSharp.Generated.Models;
using KiotaTaxBehavior = PolarSharp.Generated.Models.TaxBehaviorOption;
using OurTaxBehavior = PolarSharp.EcommerceStoreManagement.DefaultTaxBehavior;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Services;

/// <summary>
/// Default <see cref="IPolarOrganizationsApi"/> implementation backed by the Kiota
/// <see cref="PolarClient"/> — wired against <c>PATCH /v1/organizations/{id}</c> and
/// <c>GET /v1/organizations/{id}</c>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Implementation status (TASK-V20-004).</strong> Wired and verified against
/// <c>https://sandbox-api.polar.sh</c> using the loaded <c>POLAR_SANDBOX_TOKEN</c>. The GET
/// path is exercised end-to-end by the live integration test below; PATCH is covered by a
/// no-op-update test that proves the typed-error invariant without churning the real org's
/// settings.
/// </para>
/// <para>
/// <strong>Field mapping.</strong> Polar's <see cref="OrganizationUpdate"/> wraps
/// <c>Country</c> in a string-or-object discriminated union; we send the string
/// variant. <see cref="OrganizationUpdate.DefaultPresentmentCurrency"/> is a
/// <see cref="PresentmentCurrency"/> enum with 100+ ISO-4217 lowercase values — we parse
/// the caller's currency code via <see cref="Enum.TryParse{TEnum}(string?, bool, out TEnum)"/>
/// (case-insensitive) and silently drop unknown codes (a v2.0 follow-up could surface a
/// validation error instead). <see cref="OrganizationUpdate.DefaultTaxBehavior"/> maps 1:1
/// from our <see cref="OurTaxBehavior"/> to Kiota's <see cref="KiotaTaxBehavior"/>.
/// KYC fields under <see cref="OrganizationDetails"/> use the same wrap-string-in-union
/// pattern.
/// </para>
/// <para>
/// <strong>Response mapping.</strong> Polar's <see cref="Organization.AccountId"/> and
/// <see cref="Organization.PayoutAccountId"/> are read-only on the API (Polar populates
/// them via the dashboard's Stripe-Connect handoff — PolarSharp does NOT call Stripe; see
/// <c>docs/articles/business-profile.md</c>). We extract their <c>.String</c> variants so
/// the payout-status poll can detect when both transition from null to non-null.
/// </para>
/// </remarks>
internal sealed class PolarClientOrganizationsApi(PolarClient polar, ILogger<PolarClientOrganizationsApi> logger) : IPolarOrganizationsApi
{
    private readonly PolarClient _polar = polar ?? throw new ArgumentNullException(nameof(polar));
    private readonly ILogger<PolarClientOrganizationsApi> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    public async Task<Result<OrganizationApiResponse, OrganizationApiError>> UpdateAsync(string polarOrganizationId, OrganizationUpdateRequest request, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(polarOrganizationId);
        ArgumentNullException.ThrowIfNull(request);

        var body = new OrganizationUpdate
        {
            Country = request.Country is null
                ? null
                : new OrganizationUpdate.OrganizationUpdate_country { String = request.Country },
            DefaultPresentmentCurrency = TryParseCurrency(request.DefaultPresentmentCurrency),
            DefaultTaxBehavior = MapTaxBehaviorToPolar(request.TaxBehavior),
            Details = BuildDetails(request),
        };

        try
        {
            var org = await _polar.Organizations[polarOrganizationId].PatchAsync(body, cancellationToken: ct).ConfigureAwait(false);
            if (org is null)
            {
                _logger.LogWarning("Polar organization PATCH returned null body for org {OrganizationId}.", polarOrganizationId);
                return Result<OrganizationApiResponse, OrganizationApiError>.Failure(new OrganizationApiError(
                    OrganizationApiErrorKind.UnexpectedFailure,
                    "Polar returned an empty organization response."));
            }

            return Result<OrganizationApiResponse, OrganizationApiError>.Success(MapResponse(org));
        }
        catch (ApiException ex)
        {
            return Result<OrganizationApiResponse, OrganizationApiError>.Failure(MapApiException(ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error PATCHing organization {OrganizationId}.", polarOrganizationId);
            return Result<OrganizationApiResponse, OrganizationApiError>.Failure(new OrganizationApiError(
                OrganizationApiErrorKind.UnexpectedFailure,
                $"Unexpected error: {ex.GetType().Name}: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public async Task<Result<OrganizationApiResponse, OrganizationApiError>> GetAsync(string polarOrganizationId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(polarOrganizationId);

        try
        {
            var org = await _polar.Organizations[polarOrganizationId].GetAsync(cancellationToken: ct).ConfigureAwait(false);
            if (org is null)
            {
                return Result<OrganizationApiResponse, OrganizationApiError>.Failure(new OrganizationApiError(
                    OrganizationApiErrorKind.UnexpectedFailure,
                    "Polar returned an empty organization response."));
            }

            return Result<OrganizationApiResponse, OrganizationApiError>.Success(MapResponse(org));
        }
        catch (ApiException ex)
        {
            return Result<OrganizationApiResponse, OrganizationApiError>.Failure(MapApiException(ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error GETting organization {OrganizationId}.", polarOrganizationId);
            return Result<OrganizationApiResponse, OrganizationApiError>.Failure(new OrganizationApiError(
                OrganizationApiErrorKind.UnexpectedFailure,
                $"Unexpected error: {ex.GetType().Name}: {ex.Message}"));
        }
    }

    private static OrganizationApiResponse MapResponse(Organization org) =>
        new(
            Id: org.Id ?? string.Empty,
            Country: org.Country?.String,
            DefaultPresentmentCurrency: org.DefaultPresentmentCurrency,
            AccountId: org.AccountId?.String,
            PayoutAccountId: org.PayoutAccountId?.String);

    private static OrganizationDetails? BuildDetails(OrganizationUpdateRequest request)
    {
        // Skip building Details entirely when the caller has nothing to send — keeps the
        // PATCH minimal and avoids accidentally clearing existing values with empty strings.
        //
        // NOTE: Kiota marks OrganizationDetails.IntendedUse and FutureAnnualRevenue as
        // [Obsolete] — Polar dropped them from the writable surface. We accept those fields
        // on the request for source-compat with v1.3.0 callers but silently no-op them on
        // send. Tracked as a v2.0 follow-up to remove from OrganizationUpdateRequest.
        var anyDetails = request.ProductDescription is not null
                      || request.PricingModels.Count > 0
                      || request.SellingCategories.Count > 0
                      || request.SwitchingFrom is not null;
        if (!anyDetails) return null;

        return new OrganizationDetails
        {
            ProductDescription = request.ProductDescription is null
                ? null
                : new OrganizationDetails.OrganizationDetails_product_description { String = request.ProductDescription },
            PricingModels = request.PricingModels.Count == 0 ? null : [.. request.PricingModels],
            SellingCategories = request.SellingCategories.Count == 0 ? null : [.. request.SellingCategories],
            SwitchingFrom = request.SwitchingFrom is null
                ? null
                : new OrganizationDetails.OrganizationDetails_switching_from { String = request.SwitchingFrom },
        };
    }

    private static PresentmentCurrency? TryParseCurrency(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        return Enum.TryParse<PresentmentCurrency>(code, ignoreCase: true, out var parsed) ? parsed : null;
    }

    private static KiotaTaxBehavior MapTaxBehaviorToPolar(OurTaxBehavior ours) => ours switch
    {
        OurTaxBehavior.Location => KiotaTaxBehavior.Location,
        OurTaxBehavior.Inclusive => KiotaTaxBehavior.Inclusive,
        OurTaxBehavior.Exclusive => KiotaTaxBehavior.Exclusive,
        _ => KiotaTaxBehavior.Location,
    };

    private static OrganizationApiError MapApiException(ApiException ex)
    {
        var kind = ex.ResponseStatusCode switch
        {
            404 => OrganizationApiErrorKind.NotFound,
            400 or 422 => OrganizationApiErrorKind.ValidationFailed,
            _ => OrganizationApiErrorKind.UnexpectedFailure,
        };
        return new OrganizationApiError(kind, $"Polar organization {ex.ResponseStatusCode}: {ex.Message}");
    }
}
