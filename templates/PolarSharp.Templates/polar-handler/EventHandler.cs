using Microsoft.Extensions.Logging;
using PolarSharp.Webhooks;
using PolarSharp.Webhooks.Events;

namespace MyApp.Handlers;

/// <summary>
/// Handles the <see cref="OrderCreatedEvent"/> webhook received from Polar.sh.
/// </summary>
/// <remarks>
/// This handler is invoked after HMAC signature verification and event deserialization.
/// Inject your domain services via the constructor and implement your logic in
/// <see cref="HandleCoreAsync"/>.
/// <para>
/// Register this handler in <c>Program.cs</c>:
/// <code>
/// builder.Services
///     .AddPolarInfrastructure(builder.Configuration)
///     .AddPolarWebhooks()
///     .AddWebhookHandler&lt;OrderCreatedEvent, EventHandler&gt;();
/// </code>
/// </para>
/// <para>Available event data via <c>@event.Data</c>:</para>
/// <list type="bullet">
//#if (IsOrderCreatedEvent || IsOrderUpdatedEvent || IsOrderPaidEvent || IsOrderRefundedEvent)
///   <item><c>@event.Data.Id</c>             — Order ID</item>
///   <item><c>@event.Data.Number</c>         — Human-readable order number</item>
///   <item><c>@event.Data.Status</c>         — Order status (e.g. "paid", "fulfilled")</item>
///   <item><c>@event.Data.Amount</c>         — Order total (integer, minor currency units)</item>
///   <item><c>@event.Data.TaxAmount</c>      — Tax amount (minor currency units)</item>
///   <item><c>@event.Data.Currency</c>       — ISO 4217 currency code (e.g. "USD")</item>
///   <item><c>@event.Data.Channel</c>        — Sales channel ("web", "api", "embed")</item>
///   <item><c>@event.Data.BillingReason</c>  — Why this order was created</item>
///   <item><c>@event.Data.Customer</c>       — Customer details (Id, Email, Name)</item>
///   <item><c>@event.Data.Items</c>          — Line items (ProductId, ProductName, PriceAmount, Currency)</item>
///   <item><c>@event.Data.CreatedAt</c>      — UTC timestamp of order creation</item>
//#endif
//#if (IsSubscriptionCreatedEvent || IsSubscriptionActiveEvent || IsSubscriptionUpdatedEvent || IsSubscriptionCanceledEvent || IsSubscriptionUncanceledEvent || IsSubscriptionPastDueEvent || IsSubscriptionRevokedEvent)
///   <item><c>@event.Data.Id</c>                   — Subscription ID</item>
///   <item><c>@event.Data.Status</c>               — Subscription status (e.g. "active", "canceled")</item>
///   <item><c>@event.Data.CurrentPeriodStart</c>   — Billing period start (UTC)</item>
///   <item><c>@event.Data.CurrentPeriodEnd</c>     — Billing period end (UTC)</item>
///   <item><c>@event.Data.Customer</c>             — Customer details (Id, Email, Name)</item>
///   <item><c>@event.Data.Product</c>              — Product details (Id, Name)</item>
///   <item><c>@event.Data.Price</c>                — Price details (Id, Name, Amount, Currency)</item>
///   <item><c>@event.Data.CreatedAt</c>            — UTC timestamp of subscription creation</item>
//#endif
//#if (IsCheckoutCreatedEvent || IsCheckoutUpdatedEvent || IsCheckoutExpiredEvent)
///   <item><c>@event.Data.Id</c>             — Checkout session ID</item>
///   <item><c>@event.Data.Status</c>         — Session status ("created", "confirmed", "expired")</item>
///   <item><c>@event.Data.Amount</c>         — Checkout total (minor currency units)</item>
///   <item><c>@event.Data.Currency</c>       — ISO 4217 currency code</item>
///   <item><c>@event.Data.CustomerEmail</c>  — Email address of the customer</item>
///   <item><c>@event.Data.Customer</c>       — Customer details (Id, Email) if authenticated</item>
///   <item><c>@event.Data.ExpiresAt</c>      — Session expiry (UTC)</item>
///   <item><c>@event.Data.OrderId</c>        — Resulting order ID (populated on "confirmed")</item>
///   <item><c>@event.Data.CreatedAt</c>      — UTC timestamp of session creation</item>
//#endif
//#if (IsCustomerCreatedEvent || IsCustomerUpdatedEvent || IsCustomerStateChangedEvent || IsCustomerDeletedEvent)
///   <item><c>@event.Data.Id</c>                        — Customer ID</item>
///   <item><c>@event.Data.Email</c>                     — Customer email address</item>
///   <item><c>@event.Data.Name</c>                      — Customer display name</item>
///   <item><c>@event.Data.OrganizationId</c>            — Owning Polar organization ID</item>
///   <item><c>@event.Data.ActiveSubscriptionsCount</c>  — Number of active subscriptions</item>
///   <item><c>@event.Data.CreatedAt</c>                 — UTC timestamp of account creation</item>
//#endif
//#if (IsProductCreatedEvent || IsProductUpdatedEvent)
///   <item><c>@event.Data.Id</c>              — Product ID</item>
///   <item><c>@event.Data.Name</c>            — Product display name</item>
///   <item><c>@event.Data.IsArchived</c>      — Whether the product is archived</item>
///   <item><c>@event.Data.OrganizationId</c>  — Owning Polar organization ID</item>
///   <item><c>@event.Data.CreatedAt</c>       — UTC timestamp of product creation</item>
//#endif
//#if (IsBenefitCreatedEvent || IsBenefitUpdatedEvent)
///   <item><c>@event.Data.Id</c>              — Benefit ID</item>
///   <item><c>@event.Data.BenefitType</c>     — Benefit type (e.g. "license_keys", "downloadables")</item>
///   <item><c>@event.Data.Description</c>     — Human-readable benefit description</item>
///   <item><c>@event.Data.OrganizationId</c>  — Owning Polar organization ID</item>
///   <item><c>@event.Data.CreatedAt</c>       — UTC timestamp of benefit creation</item>
//#endif
//#if (IsBenefitGrantCreatedEvent || IsBenefitGrantUpdatedEvent || IsBenefitGrantCycledEvent || IsBenefitGrantRevokedEvent)
///   <item><c>@event.Data.Id</c>            — Benefit grant ID</item>
///   <item><c>@event.Data.CustomerId</c>    — Granted customer ID</item>
///   <item><c>@event.Data.BenefitId</c>     — Benefit ID</item>
///   <item><c>@event.Data.BenefitType</c>   — Benefit type string</item>
///   <item><c>@event.Data.IsGranted</c>     — Whether the benefit is currently granted</item>
///   <item><c>@event.Data.GrantedAt</c>     — UTC timestamp of grant creation</item>
//#endif
//#if (IsRefundCreatedEvent || IsRefundUpdatedEvent)
///   <item><c>@event.Data.Id</c>          — Refund ID</item>
///   <item><c>@event.Data.Amount</c>      — Refund amount (minor currency units)</item>
///   <item><c>@event.Data.Currency</c>    — ISO 4217 currency code</item>
///   <item><c>@event.Data.Reason</c>      — Refund reason (e.g. "customer_request", "fraudulent")</item>
///   <item><c>@event.Data.Status</c>      — Refund status ("pending", "succeeded", "failed")</item>
///   <item><c>@event.Data.OrderId</c>     — Originating order ID</item>
///   <item><c>@event.Data.CustomerId</c>  — Customer ID</item>
///   <item><c>@event.Data.CreatedAt</c>   — UTC timestamp of refund creation</item>
//#endif
/// </list>
/// </remarks>
public sealed class EventHandler : PolarWebhookHandlerBase<OrderCreatedEvent>
{
    // TODO: inject your domain services here
    /// <summary>
    /// Initializes a new instance of <see cref="EventHandler"/>.
    /// </summary>
    /// <param name="logger">Logger injected by the DI container.</param>
    public EventHandler(ILogger<EventHandler> logger) : base(logger) { }

    /// <inheritdoc/>
    /// <remarks>
    /// Polar delivers webhooks at-least-once. Check <c>@event.WebhookId</c> to detect
    /// and skip duplicate deliveries before performing side effects.
    /// </remarks>
    protected override Task HandleCoreAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        // TODO: implement your handler logic here.
        // Examples:
//#if (IsOrderCreatedEvent || IsOrderPaidEvent)
        //   await _orderService.FulfillAsync(OrderId.From(@event.Data.Id), ct);
        //   await _emailSender.SendConfirmationAsync(@event.Data.Customer.Email, ct);
//#endif
//#if (IsSubscriptionActiveEvent || IsSubscriptionCreatedEvent)
        //   await _entitlementService.GrantAsync(@event.Data.Customer.Id, @event.Data.Product.Id, ct);
        //   await _emailSender.SendWelcomeAsync(@event.Data.Customer.Email, ct);
//#endif
//#if (IsSubscriptionCanceledEvent || IsSubscriptionRevokedEvent)
        //   await _entitlementService.RevokeAsync(@event.Data.Customer.Id, @event.Data.Product.Id, ct);
//#endif
//#if (IsBenefitGrantCreatedEvent)
        //   await _licenseService.IssueKeyAsync(@event.Data.CustomerId, @event.Data.BenefitId, ct);
//#endif
//#if (IsBenefitGrantRevokedEvent)
        //   await _licenseService.RevokeKeyAsync(@event.Data.CustomerId, @event.Data.BenefitId, ct);
//#endif
//#if (IsRefundCreatedEvent || IsRefundUpdatedEvent)
        //   await _orderService.ProcessRefundAsync(@event.Data.OrderId, @event.Data.Amount, ct);
//#endif
        throw new NotImplementedException(
            $"Handler for {nameof(OrderCreatedEvent)} is not yet implemented. " +
            "Remove this line and add your logic above.");
    }
}
