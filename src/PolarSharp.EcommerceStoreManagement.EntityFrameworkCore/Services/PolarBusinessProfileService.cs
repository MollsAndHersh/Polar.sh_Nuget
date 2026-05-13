using System.Text.Json;
using System.Text.Json.Nodes;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Entities;
using PolarSharp.EcommerceStoreManagement.Services;
using PolarSharp.MultiTenant;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Services;

/// <summary>
/// Default <see cref="IPolarBusinessProfileService"/> implementation. Reads / writes the
/// local <see cref="TenantBusinessProfileEntity"/> for the full profile (including
/// local-only fields like street address and KYC details), and pushes the writable subset
/// to Polar via <see cref="IPolarOrganizationsApi"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Banking deep-link.</strong> <see cref="BuildBankingSetupDeepLink"/> returns a URL
/// pointing to Polar's own dashboard page where the merchant connects their bank account.
/// PolarSharp does NOT call Stripe — see commit <c>1347e01</c> and the inline XML docs on
/// <see cref="IPolarBusinessProfileService.BuildBankingSetupDeepLink"/> for the full
/// framing. The merchant clicks the link, completes setup in Polar's UI (which uses Stripe
/// internally as Polar's own payout rails), then the host calls
/// <see cref="RefreshPayoutStatusAsync"/> to detect completion.
/// </para>
/// <para>
/// <strong>Polar HTTP wiring</strong> (<see cref="IPolarOrganizationsApi"/>) ships as a
/// best-effort wrapper in v1.3.0; sandbox validation is tracked under TASK-V20-004.
/// </para>
/// </remarks>
internal sealed class PolarBusinessProfileService(
    PolarCatalogDbContext db,
    IMultiTenantContextAccessor tenantAccessor,
    IPolarOrganizationsApi polarApi,
    IOptionsMonitor<BusinessProfileOptions> options,
    TimeProvider time,
    ILogger<PolarBusinessProfileService> logger) : IPolarBusinessProfileService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly PolarCatalogDbContext _db = db ?? throw new ArgumentNullException(nameof(db));
    private readonly IMultiTenantContextAccessor _tenantAccessor = tenantAccessor ?? throw new ArgumentNullException(nameof(tenantAccessor));
    private readonly IPolarOrganizationsApi _polarApi = polarApi ?? throw new ArgumentNullException(nameof(polarApi));
    private readonly IOptionsMonitor<BusinessProfileOptions> _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly TimeProvider _time = time ?? throw new ArgumentNullException(nameof(time));
    private readonly ILogger<PolarBusinessProfileService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<Result<TenantBusinessProfile, BusinessProfileError>> GetAsync(CancellationToken ct = default)
    {
        var entity = await LoadEntityAsync(tracked: false, ct).ConfigureAwait(false);
        if (entity is null)
            return Failure(BusinessProfileErrorKind.NotFound, "Tenant has no business profile yet.");

        return Result<TenantBusinessProfile, BusinessProfileError>.Success(EntityToRecord(entity, _time));
    }

    public async Task<Result<TenantBusinessProfile, BusinessProfileError>> SaveAsync(
        TenantBusinessProfile profile,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var entity = await LoadEntityAsync(tracked: true, ct).ConfigureAwait(false);
        var isNew = entity is null;
        entity ??= new TenantBusinessProfileEntity();
        ApplyRecordToEntity(profile, entity);
        if (isNew) _db.BusinessProfiles.Add(entity);

        // Push writable subset to Polar before saving locally. If Polar rejects, surface the
        // validation error without persisting locally — keeps local state and Polar state
        // aligned. If the tenant has no Polar org id yet (pre-onboarding), skip the push.
        var tenant = ResolveCurrentTenant();
        if (tenant?.PolarOrganizationId is { Length: > 0 } orgId)
        {
            var update = new OrganizationUpdateRequest(
                Country: entity.CountryCode,
                DefaultPresentmentCurrency: entity.DefaultCurrency,
                TaxBehavior: entity.TaxBehavior,
                ProductDescription: entity.ProductDescription,
                IntendedUse: entity.IntendedUse,
                PricingModels: DeserializeStringList(entity.PricingModelsJson),
                SellingCategories: DeserializeStringList(entity.SellingCategoriesJson),
                FutureAnnualRevenue: entity.FutureAnnualRevenue,
                SwitchingFrom: entity.SwitchingFrom);

            var pushResult = await _polarApi.UpdateAsync(orgId, update, ct).ConfigureAwait(false);
            if (pushResult.IsFailure)
            {
                return pushResult.Match(
                    onSuccess: _ => Failure(BusinessProfileErrorKind.PolarApiFailure, "Unreachable"),
                    onFailure: err => Failure(MapKind(err.Kind), err.Message));
            }
        }
        else
        {
            _logger.LogDebug("SaveAsync: tenant has no Polar organization id; skipping Polar PATCH (local save only).");
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result<TenantBusinessProfile, BusinessProfileError>.Success(EntityToRecord(entity, _time));
    }

    public Uri BuildBankingSetupDeepLink()
    {
        var opts = _options.CurrentValue;
        var tenant = ResolveCurrentTenant();
        var orgId = tenant?.PolarOrganizationId ?? "";

        // Format: {DashboardBaseUrl}/dashboard/{orgId}/finance/account. The orgId may be
        // empty when called pre-onboarding — the URL still points at the right area of
        // Polar's dashboard; the merchant lands on the login page first.
        var path = string.IsNullOrEmpty(orgId)
            ? "/dashboard"
            : $"/dashboard/{Uri.EscapeDataString(orgId)}/finance/account";
        return new Uri(new Uri(opts.DashboardBaseUrl), path);
    }

    public async Task<Result<PayoutSetupStatus, BusinessProfileError>> RefreshPayoutStatusAsync(CancellationToken ct = default)
    {
        var tenant = ResolveCurrentTenant();
        if (tenant?.PolarOrganizationId is not { Length: > 0 } orgId)
            return Failure<PayoutSetupStatus>(BusinessProfileErrorKind.NotFound, "Tenant has no Polar organization id yet.");

        var entity = await LoadEntityAsync(tracked: true, ct).ConfigureAwait(false);
        if (entity is null)
            return Failure<PayoutSetupStatus>(BusinessProfileErrorKind.NotFound, "Tenant has no business profile row yet.");

        var apiResult = await _polarApi.GetAsync(orgId, ct).ConfigureAwait(false);
        if (apiResult.IsFailure)
        {
            return apiResult.Match(
                onSuccess: _ => Failure<PayoutSetupStatus>(BusinessProfileErrorKind.PolarApiFailure, "Unreachable"),
                onFailure: err => Failure<PayoutSetupStatus>(MapKind(err.Kind), err.Message));
        }

        var response = apiResult.Match(onSuccess: r => r, onFailure: _ => throw new InvalidOperationException("Unreachable"));
        entity.StripeConnectAccountId = response.AccountId;
        entity.PayoutAccountId = response.PayoutAccountId;
        entity.PayoutStatus = ComputePayoutStatus(response.AccountId, response.PayoutAccountId);
        entity.PayoutStatusLastCheckedAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result<PayoutSetupStatus, BusinessProfileError>.Success(entity.PayoutStatus);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<TenantBusinessProfileEntity?> LoadEntityAsync(bool tracked, CancellationToken ct)
    {
        try
        {
            var query = _db.BusinessProfiles.AsQueryable();
            if (!tracked) query = query.AsNoTracking();
            return await query.FirstOrDefaultAsync(ct).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            // Defensive: catches "no current tenant" scenarios. Surfaced as NotFound to callers.
            _logger.LogDebug(ex, "BusinessProfile: lookup raised InvalidOperationException; returning null.");
            return null;
        }
    }

    private PolarTenantInfo? ResolveCurrentTenant() =>
        _tenantAccessor.MultiTenantContext?.TenantInfo as PolarTenantInfo;

    private static PayoutSetupStatus ComputePayoutStatus(string? accountId, string? payoutAccountId) =>
        (accountId, payoutAccountId) switch
        {
            (null or "", null or "") => PayoutSetupStatus.NotStarted,
            (_, null or "") => PayoutSetupStatus.InProgress,
            _ => PayoutSetupStatus.Ready,
        };

    private static BusinessProfileErrorKind MapKind(OrganizationApiErrorKind kind) => kind switch
    {
        OrganizationApiErrorKind.NotFound => BusinessProfileErrorKind.NotFound,
        OrganizationApiErrorKind.ValidationFailed => BusinessProfileErrorKind.PolarValidation,
        _ => BusinessProfileErrorKind.PolarApiFailure,
    };

    private static Result<TenantBusinessProfile, BusinessProfileError> Failure(BusinessProfileErrorKind kind, string message) =>
        Result<TenantBusinessProfile, BusinessProfileError>.Failure(new BusinessProfileError(kind, message));

    private static Result<T, BusinessProfileError> Failure<T>(BusinessProfileErrorKind kind, string message) =>
        Result<T, BusinessProfileError>.Failure(new BusinessProfileError(kind, message));

    private static IReadOnlyList<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOpts) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static void ApplyRecordToEntity(TenantBusinessProfile p, TenantBusinessProfileEntity e)
    {
        // TenantId intentionally NOT set here — StampNewEntities (in TenantAwareDbContextBase) sets it
        // from the current Finbuckle tenant context on SaveChanges. In production tenant.Id ==
        // p.Id (the Polar org id), so the two are interchangeable. In test scenarios where they
        // differ, deferring to StampNewEntities keeps the global filter happy.
        e.OrganizationName = p.Name;
        e.CountryCode = p.Country ?? "";
        e.DefaultCurrency = p.DefaultPresentmentCurrency ?? "";
        e.TaxBehavior = p.TaxBehavior;
        e.StreetLine1 = p.StreetLine1;
        e.StreetLine2 = p.StreetLine2;
        e.City = p.City;
        e.StateOrProvince = p.StateOrProvince;
        e.PostalCode = p.PostalCode;
        e.ProductDescription = p.ProductDescription;
        e.IntendedUse = p.IntendedUse;
        e.PricingModelsJson = JsonSerializer.Serialize(p.PricingModels, JsonOpts);
        e.SellingCategoriesJson = JsonSerializer.Serialize(p.SellingCategories, JsonOpts);
        e.FutureAnnualRevenue = p.FutureAnnualRevenue;
        e.SwitchingFrom = p.SwitchingFrom;
        e.LegalEntityJson = p.LegalEntity?.ToJsonString();
        e.StripeConnectAccountId = p.AccountId;
        e.PayoutAccountId = p.PayoutAccountId;
        e.PayoutStatus = p.PayoutStatus;
        e.PayoutStatusLastCheckedAt = p.PayoutStatusLastCheckedAt;
        e.TranslationProvider = p.TranslationProvider;
        e.TranslationApiKeyEncrypted = p.TranslationApiKeyEncrypted;
        e.TranslationModel = p.TranslationModel;
        e.TranslationEndpoint = p.TranslationEndpoint;
        e.MasterLanguage = p.MasterLanguage;
        e.SupportedLanguagesJson = JsonSerializer.Serialize(p.SupportedLanguages, JsonOpts);
        e.AutoTranslateOnSave = p.AutoTranslateOnSave;
        e.AllowFakeData = p.AllowFakeData;
        e.IsFakeData = p.IsFakeData;
    }

    private static TenantBusinessProfile EntityToRecord(TenantBusinessProfileEntity e, TimeProvider _) =>
        new()
        {
            Id = e.TenantId,
            Name = e.OrganizationName,
            Slug = e.OrganizationName,                   // entity doesn't carry slug; reuse name
            Country = e.CountryCode,
            DefaultPresentmentCurrency = e.DefaultCurrency,
            CreatedAt = DateTimeOffset.UtcNow,           // not stored on entity; use synthetic
            TaxBehavior = e.TaxBehavior,
            StreetLine1 = e.StreetLine1,
            StreetLine2 = e.StreetLine2,
            City = e.City,
            StateOrProvince = e.StateOrProvince,
            PostalCode = e.PostalCode,
            ProductDescription = e.ProductDescription,
            IntendedUse = e.IntendedUse,
            PricingModels = DeserializeStringList(e.PricingModelsJson),
            SellingCategories = DeserializeStringList(e.SellingCategoriesJson),
            FutureAnnualRevenue = e.FutureAnnualRevenue,
            SwitchingFrom = e.SwitchingFrom,
            LegalEntity = string.IsNullOrEmpty(e.LegalEntityJson) ? null : JsonNode.Parse(e.LegalEntityJson),
            AccountId = e.StripeConnectAccountId,
            PayoutAccountId = e.PayoutAccountId,
            PayoutStatus = e.PayoutStatus,
            PayoutStatusLastCheckedAt = e.PayoutStatusLastCheckedAt,
            TranslationProvider = e.TranslationProvider,
            TranslationApiKeyEncrypted = e.TranslationApiKeyEncrypted,
            TranslationModel = e.TranslationModel,
            TranslationEndpoint = e.TranslationEndpoint,
            MasterLanguage = e.MasterLanguage,
            SupportedLanguages = DeserializeStringList(e.SupportedLanguagesJson),
            AutoTranslateOnSave = e.AutoTranslateOnSave,
            AllowFakeData = e.AllowFakeData,
            IsFakeData = e.IsFakeData,
        };
}

/// <summary>Options for <see cref="PolarBusinessProfileService"/>. Bound from <c>PolarSharp:EcommerceStoreManagement:BusinessProfile</c>.</summary>
public sealed class BusinessProfileOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "PolarSharp:EcommerceStoreManagement:BusinessProfile";

    /// <summary>Base URL of Polar's dashboard. The banking-setup deep link is built relative to this. Defaults to the production URL.</summary>
    public string DashboardBaseUrl { get; set; } = "https://polar.sh";
}
