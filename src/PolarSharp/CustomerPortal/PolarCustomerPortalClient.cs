using Microsoft.Kiota.Http.HttpClientLibrary;
using PolarSharp.Generated;
using PolarSharp.Generated.V1.CustomerPortal;
using PolarSharp.Generated.V1.CustomerPortal.BenefitGrants;
using PolarSharp.Generated.V1.CustomerPortal.Customers;
using PolarSharp.Generated.V1.CustomerPortal.Downloadables;
using PolarSharp.Generated.V1.CustomerPortal.LicenseKeys;
using PolarSharp.Generated.V1.CustomerPortal.Members;
using PolarSharp.Generated.V1.CustomerPortal.Meters;
using PolarSharp.Generated.V1.CustomerPortal.Orders;
using PolarSharp.Generated.V1.CustomerPortal.Organizations;
using PolarSharp.Generated.V1.CustomerPortal.Seats;
using PolarSharp.Generated.V1.CustomerPortal.Subscriptions;

namespace PolarSharp.CustomerPortal;

/// <summary>
/// A Polar API client scoped to Customer Portal endpoints, authenticated with a Customer Access Token.
/// </summary>
/// <remarks>
/// <para>
/// Exposes only the <c>/v1/customer-portal/*</c> resource surface.
/// Do not use this client with Organization Access Tokens (OAT) — doing so would grant
/// customer-portal endpoints organization-level privileges, which is a security violation.
/// </para>
/// <para>
/// Obtain an instance via <see cref="PolarClient.CreateCustomerPortalClient"/>, passing a token
/// created by <see cref="PolarClient.CustomerSessions"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Server-side: mint a customer session using the OAT-authenticated PolarClient
/// var session = await polar.CustomerSessions.PostAsync(new CreateCustomerSession { CustomerId = "cus_xxx" });
///
/// // Then give the portal client to the customer's session:
/// var portal = polar.CreateCustomerPortalClient(session.Token);
/// var orders = await portal.Orders.GetAsync();
/// </code>
/// </example>
public sealed class PolarCustomerPortalClient
{
    private readonly PolarApiClient _inner;

    /// <summary>
    /// Initialises a new <see cref="PolarCustomerPortalClient"/> authenticated with the supplied customer token.
    /// </summary>
    /// <param name="customerToken">
    /// A Customer Access Token returned by <c>CustomerSessions.PostAsync</c>.
    /// Must not be <see langword="null"/> or empty.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="customerToken"/> is <see langword="null"/> or whitespace.
    /// </exception>
    internal PolarCustomerPortalClient(string customerToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerToken);

        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", customerToken);
        httpClient.BaseAddress = new Uri("https://api.polar.sh/v1/customer-portal/");

        var adapter = new HttpClientRequestAdapter(
            new Microsoft.Kiota.Abstractions.Authentication.AnonymousAuthenticationProvider(),
            httpClient: httpClient);
        _inner = new PolarApiClient(adapter);
    }

    // ── Customer Portal resources ──────────────────────────────────────────

    /// <summary>Gets the BenefitGrants portal resource for querying granted benefits.</summary>
    public BenefitGrantsRequestBuilder BenefitGrants => _inner.V1.CustomerPortal.BenefitGrants;

    /// <summary>Gets the Customers portal resource for reading/updating the authenticated customer profile.</summary>
    public CustomersRequestBuilder Customers => _inner.V1.CustomerPortal.Customers;

    /// <summary>Gets the Downloadables portal resource for accessing downloadable files.</summary>
    public DownloadablesRequestBuilder Downloadables => _inner.V1.CustomerPortal.Downloadables;

    /// <summary>Gets the LicenseKeys portal resource for validating and listing license keys.</summary>
    public LicenseKeysRequestBuilder LicenseKeys => _inner.V1.CustomerPortal.LicenseKeys;

    /// <summary>Gets the Members portal resource for listing organization members visible to the customer.</summary>
    public MembersRequestBuilder Members => _inner.V1.CustomerPortal.Members;

    /// <summary>Gets the Meters portal resource for viewing usage meter values.</summary>
    public MetersRequestBuilder Meters => _inner.V1.CustomerPortal.Meters;

    /// <summary>Gets the Orders portal resource for listing the customer's orders.</summary>
    public OrdersRequestBuilder Orders => _inner.V1.CustomerPortal.Orders;

    /// <summary>Gets the Organizations portal resource for viewing organization details.</summary>
    public OrganizationsRequestBuilder Organizations => _inner.V1.CustomerPortal.Organizations;

    /// <summary>Gets the Seats portal resource for listing seat-based entitlements.</summary>
    public SeatsRequestBuilder Seats => _inner.V1.CustomerPortal.Seats;

    /// <summary>Gets the Subscriptions portal resource for listing and managing the customer's subscriptions.</summary>
    public SubscriptionsRequestBuilder Subscriptions => _inner.V1.CustomerPortal.Subscriptions;
}
