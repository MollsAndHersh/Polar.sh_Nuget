using Microsoft.Kiota.Http.HttpClientLibrary;
using PolarSharp.CustomerPortal;
using PolarSharp.Generated;
using PolarSharp.Generated.V1.BenefitGrants;
using PolarSharp.Generated.V1.Benefits;
using PolarSharp.Generated.V1.CheckoutLinks;
using PolarSharp.Generated.V1.Checkouts;
using PolarSharp.Generated.V1.CustomerMeters;
using PolarSharp.Generated.V1.CustomerPortal;
using PolarSharp.Generated.V1.Customers;
using PolarSharp.Generated.V1.CustomerSeats;
using PolarSharp.Generated.V1.CustomerSessions;
using PolarSharp.Generated.V1.CustomFields;
using PolarSharp.Generated.V1.Discounts;
using PolarSharp.Generated.V1.Disputes;
using PolarSharp.Generated.V1.Events;
using PolarSharp.Generated.V1.EventTypes;
using PolarSharp.Generated.V1.Files;
using PolarSharp.Generated.V1.LicenseKeys;
using PolarSharp.Generated.V1.Members;
using PolarSharp.Generated.V1.Meters;
using PolarSharp.Generated.V1.Metrics;
using PolarSharp.Generated.V1.Oauth2;
using PolarSharp.Generated.V1.Orders;
using PolarSharp.Generated.V1.OrganizationAccessTokens;
using PolarSharp.Generated.V1.Organizations;
using PolarSharp.Generated.V1.Payments;
using PolarSharp.Generated.V1.Products;
using PolarSharp.Generated.V1.Refunds;
using PolarSharp.Generated.V1.Subscriptions;
using PolarSharp.Generated.V1.Webhooks;
using PolarSharp.Versioning;

namespace PolarSharp;

/// <summary>
/// The primary entry point for accessing the Polar.sh API.
/// Exposes typed resource properties for every Polar API area.
/// </summary>
/// <remarks>
/// <para>
/// Register as a singleton via <see cref="Extensions.ServiceCollectionExtensions.AddPolarInfrastructure"/>
/// and inject wherever Polar API calls are needed.
/// </para>
/// <para>
/// <strong>Thread safety:</strong> All resource properties are lazy and thread-safe.
/// <see cref="PolarClient"/> is safe to call concurrently from any number of threads.
/// </para>
/// <para>
/// <strong>SDK version:</strong> Built against Polar API version
/// <see cref="GeneratedAgainstVersion"/>. Configure <c>PolarSharp:ApiVersion</c> in
/// <c>appsettings.json</c> to pin a specific schema version.
/// </para>
/// </remarks>
/// <example>
/// Injecting in a Minimal API endpoint:
/// <code>
/// app.MapGet("/orders/{id}", async (string id, PolarClient polar, CancellationToken ct) =>
///     (await polar.Orders.ById(id).GetAsync(cancellationToken: ct))
///         is { } order
///         ? Results.Ok(order)
///         : Results.NotFound());
/// </code>
/// </example>
public sealed class PolarClient
{
    private readonly PolarApiClient _inner;

    /// <summary>
    /// The Polar API version date the bundled generated client was built against.
    /// </summary>
    /// <value>
    /// An ISO date string in <c>YYYY-MM-DD</c> format, e.g. <c>"2025-01-15"</c>.
    /// </value>
    public static string GeneratedAgainstVersion => PolarApiMetadata.GeneratedAgainstVersion;

    /// <summary>
    /// Initialises a new <see cref="PolarClient"/> backed by the given <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="httpClient">
    /// A pre-configured <see cref="HttpClient"/> supplied by the DI-registered
    /// <c>IHttpClientFactory</c> named <c>"PolarSharp"</c>.
    /// Must not be <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="httpClient"/> is <see langword="null"/>.
    /// </exception>
    public PolarClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        var adapter = new HttpClientRequestAdapter(new Microsoft.Kiota.Abstractions.Authentication.AnonymousAuthenticationProvider(), httpClient: httpClient);
        _inner = new PolarApiClient(adapter);
    }

    // ── Core API resources ─────────────────────────────────────────────────

    /// <summary>Gets the Orders resource builder for listing and retrieving orders.</summary>
    public OrdersRequestBuilder Orders => _inner.V1.Orders;

    /// <summary>Gets the Subscriptions resource builder for managing subscription lifecycle.</summary>
    public SubscriptionsRequestBuilder Subscriptions => _inner.V1.Subscriptions;

    /// <summary>Gets the Customers resource builder for creating and managing customers.</summary>
    public CustomersRequestBuilder Customers => _inner.V1.Customers;

    /// <summary>Gets the Products resource builder for managing products and pricing.</summary>
    public ProductsRequestBuilder Products => _inner.V1.Products;

    /// <summary>Gets the Checkouts resource builder for creating checkout sessions.</summary>
    public CheckoutsRequestBuilder Checkouts => _inner.V1.Checkouts;

    /// <summary>Gets the CheckoutLinks resource builder for creating shareable checkout links.</summary>
    public CheckoutLinksRequestBuilder CheckoutLinks => _inner.V1.CheckoutLinks;

    /// <summary>Gets the Benefits resource builder for managing product benefits.</summary>
    public BenefitsRequestBuilder Benefits => _inner.V1.Benefits;

    /// <summary>Gets the BenefitGrants resource builder for querying granted benefits.</summary>
    public BenefitGrantsRequestBuilder BenefitGrants => _inner.V1.BenefitGrants;

    /// <summary>Gets the Discounts resource builder for creating and managing discounts.</summary>
    public DiscountsRequestBuilder Discounts => _inner.V1.Discounts;

    /// <summary>Gets the Refunds resource builder for initiating and querying refunds.</summary>
    public RefundsRequestBuilder Refunds => _inner.V1.Refunds;

    /// <summary>Gets the LicenseKeys resource builder for validating and managing license keys.</summary>
    public LicenseKeysRequestBuilder LicenseKeys => _inner.V1.LicenseKeys;

    /// <summary>Gets the Meters resource builder for managing usage-based billing meters.</summary>
    public MetersRequestBuilder Meters => _inner.V1.Meters;

    /// <summary>Gets the CustomerMeters resource builder for querying per-customer meter values.</summary>
    public CustomerMetersRequestBuilder CustomerMeters => _inner.V1.CustomerMeters;

    /// <summary>Gets the Events resource builder for ingesting and listing usage events.</summary>
    public EventsRequestBuilder Events => _inner.V1.Events;

    /// <summary>Gets the EventTypes resource builder for querying available event type definitions.</summary>
    public EventTypesRequestBuilder EventTypes => _inner.V1.EventTypes;

    /// <summary>Gets the CustomerSeats resource builder for managing seat-based entitlements.</summary>
    public CustomerSeatsRequestBuilder CustomerSeats => _inner.V1.CustomerSeats;

    /// <summary>Gets the CustomerSessions resource builder for creating Customer Portal sessions.</summary>
    public CustomerSessionsRequestBuilder CustomerSessions => _inner.V1.CustomerSessions;

    /// <summary>Gets the Webhooks resource builder for managing webhook endpoints.</summary>
    public WebhooksRequestBuilder Webhooks => _inner.V1.Webhooks;

    /// <summary>Gets the Organizations resource builder for querying the authenticated organization.</summary>
    public OrganizationsRequestBuilder Organizations => _inner.V1.Organizations;

    /// <summary>Gets the Members resource builder for listing organization members.</summary>
    public MembersRequestBuilder Members => _inner.V1.Members;

    /// <summary>Gets the Files resource builder for uploading and managing files.</summary>
    public FilesRequestBuilder Files => _inner.V1.Files;

    /// <summary>Gets the CustomFields resource builder for managing custom field definitions.</summary>
    public CustomFieldsRequestBuilder CustomFields => _inner.V1.CustomFields;

    /// <summary>Gets the OrganizationAccessTokens resource builder for managing API tokens.</summary>
    public OrganizationAccessTokensRequestBuilder OrganizationAccessTokens => _inner.V1.OrganizationAccessTokens;

    /// <summary>Gets the Oauth2 resource builder for OAuth 2.0 flows.</summary>
    public Oauth2RequestBuilder Oauth2 => _inner.V1.Oauth2;

    /// <summary>Gets the Payments resource builder for querying payment records.</summary>
    public PaymentsRequestBuilder Payments => _inner.V1.Payments;

    /// <summary>Gets the Disputes resource builder for querying dispute records.</summary>
    public DisputesRequestBuilder Disputes => _inner.V1.Disputes;

    /// <summary>Gets the Metrics resource builder for querying analytics metrics.</summary>
    public MetricsRequestBuilder Metrics => _inner.V1.Metrics;

    // ── Customer Portal ────────────────────────────────────────────────────

    /// <summary>
    /// Gets the CustomerPortal resource builder.
    /// </summary>
    /// <remarks>
    /// The Customer Portal surface is authenticated with a Customer Access Token
    /// (not an Organization Access Token). Create a session via
    /// <see cref="CustomerSessions"/> and use the returned token on a dedicated
    /// <see cref="PolarCustomerPortalClient"/> to keep the security boundary intact.
    /// </remarks>
    public CustomerPortalRequestBuilder CustomerPortal => _inner.V1.CustomerPortal;

    /// <summary>
    /// Creates a <see cref="PolarCustomerPortalClient"/> scoped to the given customer session token.
    /// </summary>
    /// <param name="customerToken">
    /// A Customer Access Token obtained from <see cref="CustomerSessions"/>.
    /// Must not be <see langword="null"/> or empty.
    /// </param>
    /// <returns>
    /// A <see cref="PolarCustomerPortalClient"/> that authenticates requests using the
    /// supplied <paramref name="customerToken"/>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="customerToken"/> is <see langword="null"/> or
    /// consists only of white-space characters.
    /// </exception>
    /// <remarks>
    /// <strong>Security boundary:</strong> The returned client uses a separate
    /// <see cref="HttpClient"/> instance configured only for Customer Portal endpoints.
    /// Organization Access Tokens and Customer Access Tokens never share an
    /// <see cref="HttpClient"/>.
    /// <para>
    /// <strong>Always dispose the returned client</strong> (e.g. <c>using var portal = …;</c>) —
    /// each instance owns its own <see cref="HttpClient"/> and connection pool, so leaking
    /// instances leaks pools.
    /// </para>
    /// </remarks>
    public PolarCustomerPortalClient CreateCustomerPortalClient(string customerToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerToken);
        return new PolarCustomerPortalClient(customerToken);
    }
}
