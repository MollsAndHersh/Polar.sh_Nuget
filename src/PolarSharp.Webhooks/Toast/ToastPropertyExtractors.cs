using PolarSharp.Webhooks.Events;

namespace PolarSharp.Webhooks.Toast;

/// <summary>
/// Provides AOT-safe, static property extractors that convert each known
/// <see cref="WebhookEvent"/> subtype into a flat string-string dictionary of
/// <c>{Token}</c> values for toast message template substitution.
/// </summary>
/// <remarks>
/// <para>
/// Every extractor is a pure <c>static Func</c> registered at type-initialization time.
/// No reflection, no expression compilation — the dictionary is populated at startup once
/// and then read-only at dispatch time.
/// </para>
/// <para>
/// Unknown event types (not in the registry) return an empty dictionary. An unknown token
/// in a template (one that is not in the returned dictionary) is left as-is in the rendered
/// message; a <c>Debug</c> log entry is emitted for each unresolved token.
/// </para>
/// </remarks>
internal static class ToastPropertyExtractors
{
    private static readonly IReadOnlyDictionary<string, Func<WebhookEvent, IReadOnlyDictionary<string, string>>>
        Extractors = new Dictionary<string, Func<WebhookEvent, IReadOnlyDictionary<string, string>>>
        {
            ["order.created"] = static e =>
            {
                var evt = (OrderCreatedEvent)e;
                return Tokens(
                    ("OrderId",       evt.Data.Id),
                    ("OrderNumber",   evt.Data.Number ?? ""),
                    ("CustomerEmail", evt.Data.Customer?.Email ?? ""),
                    ("CustomerName",  evt.Data.Customer?.Name ?? ""),
                    ("TotalAmount",   FormatAmount(evt.Data.Amount, evt.Data.Currency)),
                    ("Currency",      evt.Data.Currency ?? ""),
                    ("Channel",       evt.Data.Channel ?? ""),
                    ("ProductName",   evt.Data.Items.FirstOrDefault()?.ProductName ?? ""));
            },
            ["order.updated"] = static e =>
            {
                var evt = (OrderUpdatedEvent)e;
                return Tokens(
                    ("OrderId",       evt.Data.Id),
                    ("OrderNumber",   evt.Data.Number ?? ""),
                    ("CustomerEmail", evt.Data.Customer?.Email ?? ""),
                    ("TotalAmount",   FormatAmount(evt.Data.Amount, evt.Data.Currency)),
                    ("Currency",      evt.Data.Currency ?? ""));
            },
            ["order.paid"] = static e =>
            {
                var evt = (OrderPaidEvent)e;
                return Tokens(
                    ("OrderId",       evt.Data.Id),
                    ("OrderNumber",   evt.Data.Number ?? ""),
                    ("CustomerEmail", evt.Data.Customer?.Email ?? ""),
                    ("TotalAmount",   FormatAmount(evt.Data.Amount, evt.Data.Currency)),
                    ("Currency",      evt.Data.Currency ?? ""));
            },
            ["order.refunded"] = static e =>
            {
                var evt = (OrderRefundedEvent)e;
                return Tokens(
                    ("OrderId",       evt.Data.Id),
                    ("OrderNumber",   evt.Data.Number ?? ""),
                    ("CustomerEmail", evt.Data.Customer?.Email ?? ""),
                    ("TotalAmount",   FormatAmount(evt.Data.Amount, evt.Data.Currency)),
                    ("Currency",      evt.Data.Currency ?? ""));
            },
            ["subscription.created"] = static e =>
            {
                var evt = (SubscriptionCreatedEvent)e;
                return Tokens(
                    ("SubscriptionId", evt.Data.Id),
                    ("CustomerEmail",  evt.Data.Customer?.Email ?? ""),
                    ("ProductName",    evt.Data.Product?.Name ?? ""),
                    ("PlanName",       evt.Data.Price?.Name ?? ""));
            },
            ["subscription.active"] = static e =>
            {
                var evt = (SubscriptionActiveEvent)e;
                return Tokens(
                    ("SubscriptionId", evt.Data.Id),
                    ("CustomerEmail",  evt.Data.Customer?.Email ?? ""),
                    ("ProductName",    evt.Data.Product?.Name ?? ""),
                    ("PlanName",       evt.Data.Price?.Name ?? ""));
            },
            ["subscription.updated"] = static e =>
            {
                var evt = (SubscriptionUpdatedEvent)e;
                return Tokens(
                    ("SubscriptionId", evt.Data.Id),
                    ("CustomerEmail",  evt.Data.Customer?.Email ?? ""),
                    ("ProductName",    evt.Data.Product?.Name ?? ""));
            },
            ["subscription.canceled"] = static e =>
            {
                var evt = (SubscriptionCanceledEvent)e;
                return Tokens(
                    ("SubscriptionId", evt.Data.Id),
                    ("CustomerEmail",  evt.Data.Customer?.Email ?? ""),
                    ("ProductName",    evt.Data.Product?.Name ?? ""),
                    ("PlanName",       evt.Data.Price?.Name ?? ""));
            },
            ["subscription.uncanceled"] = static e =>
            {
                var evt = (SubscriptionUncanceledEvent)e;
                return Tokens(
                    ("SubscriptionId", evt.Data.Id),
                    ("CustomerEmail",  evt.Data.Customer?.Email ?? ""),
                    ("ProductName",    evt.Data.Product?.Name ?? ""));
            },
            ["subscription.past_due"] = static e =>
            {
                var evt = (SubscriptionPastDueEvent)e;
                return Tokens(
                    ("SubscriptionId", evt.Data.Id),
                    ("CustomerEmail",  evt.Data.Customer?.Email ?? ""),
                    ("ProductName",    evt.Data.Product?.Name ?? ""));
            },
            ["subscription.revoked"] = static e =>
            {
                var evt = (SubscriptionRevokedEvent)e;
                return Tokens(
                    ("SubscriptionId", evt.Data.Id),
                    ("CustomerEmail",  evt.Data.Customer?.Email ?? ""),
                    ("ProductName",    evt.Data.Product?.Name ?? ""));
            },
            ["checkout.created"] = static e =>
            {
                var evt = (CheckoutCreatedEvent)e;
                return Tokens(
                    ("CustomerEmail", evt.Data.CustomerEmail ?? ""),
                    ("TotalAmount",   FormatAmount(evt.Data.Amount, evt.Data.Currency)),
                    ("Currency",      evt.Data.Currency ?? ""));
            },
            ["checkout.updated"] = static e =>
            {
                var evt = (CheckoutUpdatedEvent)e;
                return Tokens(
                    ("CustomerEmail", evt.Data.CustomerEmail ?? ""),
                    ("TotalAmount",   FormatAmount(evt.Data.Amount, evt.Data.Currency)),
                    ("Currency",      evt.Data.Currency ?? ""));
            },
            ["checkout.expired"] = static e =>
            {
                var evt = (CheckoutExpiredEvent)e;
                return Tokens(
                    ("CustomerEmail", evt.Data.CustomerEmail ?? ""),
                    ("TotalAmount",   FormatAmount(evt.Data.Amount, evt.Data.Currency)),
                    ("Currency",      evt.Data.Currency ?? ""));
            },
            ["customer.created"] = static e =>
            {
                var evt = (CustomerCreatedEvent)e;
                return Tokens(
                    ("CustomerId",    evt.Data.Id),
                    ("CustomerEmail", evt.Data.Email ?? ""),
                    ("CustomerName",  evt.Data.Name ?? ""));
            },
            ["customer.updated"] = static e =>
            {
                var evt = (CustomerUpdatedEvent)e;
                return Tokens(
                    ("CustomerId",    evt.Data.Id),
                    ("CustomerEmail", evt.Data.Email ?? ""),
                    ("CustomerName",  evt.Data.Name ?? ""));
            },
            ["customer.state_changed"] = static e =>
            {
                var evt = (CustomerStateChangedEvent)e;
                return Tokens(
                    ("CustomerId",    evt.Data.Id),
                    ("CustomerEmail", evt.Data.Email ?? ""));
            },
            ["customer.deleted"] = static e =>
            {
                var evt = (CustomerDeletedEvent)e;
                return Tokens(
                    ("CustomerId",    evt.Data.Id),
                    ("CustomerEmail", evt.Data.Email ?? ""));
            },
            ["product.created"] = static e =>
            {
                var evt = (ProductCreatedEvent)e;
                return Tokens(
                    ("ProductId",   evt.Data.Id),
                    ("ProductName", evt.Data.Name ?? ""));
            },
            ["product.updated"] = static e =>
            {
                var evt = (ProductUpdatedEvent)e;
                return Tokens(
                    ("ProductId",   evt.Data.Id),
                    ("ProductName", evt.Data.Name ?? ""));
            },
            ["benefit.created"] = static e =>
            {
                var evt = (BenefitCreatedEvent)e;
                return Tokens(
                    ("BenefitId",   evt.Data.Id),
                    ("BenefitType", evt.Data.BenefitType ?? ""),
                    ("Description", evt.Data.Description ?? ""));
            },
            ["benefit.updated"] = static e =>
            {
                var evt = (BenefitUpdatedEvent)e;
                return Tokens(
                    ("BenefitId",   evt.Data.Id),
                    ("BenefitType", evt.Data.BenefitType ?? ""),
                    ("Description", evt.Data.Description ?? ""));
            },
            ["benefit_grant.created"] = static e =>
            {
                var evt = (BenefitGrantCreatedEvent)e;
                return Tokens(
                    ("CustomerId",  evt.Data.CustomerId ?? ""),
                    ("BenefitId",   evt.Data.BenefitId ?? ""),
                    ("BenefitType", evt.Data.BenefitType ?? ""));
            },
            ["benefit_grant.updated"] = static e =>
            {
                var evt = (BenefitGrantUpdatedEvent)e;
                return Tokens(
                    ("CustomerId",  evt.Data.CustomerId ?? ""),
                    ("BenefitType", evt.Data.BenefitType ?? ""));
            },
            ["benefit_grant.cycled"] = static e =>
            {
                var evt = (BenefitGrantCycledEvent)e;
                return Tokens(
                    ("CustomerId",  evt.Data.CustomerId ?? ""),
                    ("BenefitType", evt.Data.BenefitType ?? ""));
            },
            ["benefit_grant.revoked"] = static e =>
            {
                var evt = (BenefitGrantRevokedEvent)e;
                return Tokens(
                    ("CustomerId",  evt.Data.CustomerId ?? ""),
                    ("BenefitId",   evt.Data.BenefitId ?? ""),
                    ("BenefitType", evt.Data.BenefitType ?? ""));
            },
            ["refund.created"] = static e =>
            {
                var evt = (RefundCreatedEvent)e;
                return Tokens(
                    ("RefundId",    evt.Data.Id),
                    ("OrderId",     evt.Data.OrderId ?? ""),
                    ("TotalAmount", FormatAmount(evt.Data.Amount, evt.Data.Currency)),
                    ("Currency",    evt.Data.Currency ?? ""),
                    ("CustomerId",  evt.Data.CustomerId ?? ""));
            },
            ["refund.updated"] = static e =>
            {
                var evt = (RefundUpdatedEvent)e;
                return Tokens(
                    ("RefundId",     evt.Data.Id),
                    ("OrderId",      evt.Data.OrderId ?? ""),
                    ("TotalAmount",  FormatAmount(evt.Data.Amount, evt.Data.Currency)),
                    ("Currency",     evt.Data.Currency ?? ""),
                    ("RefundStatus", evt.Data.Status ?? ""));
            },
        };

    /// <summary>
    /// Extracts a flat string token dictionary from the given <paramref name="event"/>.
    /// </summary>
    /// <param name="event">The verified Polar webhook event.</param>
    /// <returns>
    /// A dictionary of <c>{TokenName}</c> → value pairs for template substitution.
    /// Returns <see cref="System.Collections.Immutable.ImmutableDictionary{TKey,TValue}.Empty"/>
    /// for unrecognized event types.
    /// </returns>
    public static IReadOnlyDictionary<string, string> Extract(WebhookEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        return Extractors.TryGetValue(@event.Type, out var extractor)
            ? extractor(@event)
            : System.Collections.Immutable.ImmutableDictionary<string, string>.Empty;
    }

    private static Dictionary<string, string> Tokens(
        params (string Key, string Value)[] pairs)
    {
        var dict = new Dictionary<string, string>(pairs.Length, StringComparer.Ordinal);
        foreach (var (key, value) in pairs)
            dict[key] = value;
        return dict;
    }

    private static string FormatAmount(int? amount, string? currency)
    {
        if (amount is null) return "";
        var value = amount.Value / 100m;
        return currency is null ? value.ToString("F2") : $"{value:F2} {currency}";
    }
}
