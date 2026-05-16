using PolarSharp.BaseEntities;

namespace PolarSharp.CustomerGraph;

/// <summary>
/// Feeds source events (Polar webhooks, EF SaveChanges interceptions, IP capture events,
/// host-defined custom events) into the customer graph. Implementations are typically
/// hosted-service driven for eventual consistency; the projector is the WRITE side, the
/// <see cref="ICustomerGraphQueryClient"/> is the READ side.
/// </summary>
public interface ICustomerGraphProjector
{
    /// <summary>Records a customer's first observation (creates the customer node if absent).</summary>
    Task UpsertCustomerAsync(CustomerNodeInput customer, CancellationToken ct = default);

    /// <summary>Records a customer's purchase of a product (adds the PURCHASED edge + updates aggregate fields).</summary>
    Task RecordPurchaseAsync(string customerId, string productId, int quantity, decimal unitAmount, string currency, DateTimeOffset occurredAt, CancellationToken ct = default);

    /// <summary>
    /// Records a customer's IP usage (creates the IP node + USED_IP edge). No-ops when the
    /// tenant's <see cref="IpCaptureMode"/> is <c>Disabled</c>.
    /// </summary>
    Task RecordIpUsageAsync(string customerId, string ipHash, DateTimeOffset occurredAt, CancellationToken ct = default);

    /// <summary>Updates a customer's host-defined tags (used for "active", "vip", "fraud-flagged" classifications).</summary>
    Task SetCustomerTagsAsync(string customerId, IReadOnlyList<string> tags, CancellationToken ct = default);

    /// <summary>Removes a customer node + all incident edges (GDPR right-to-be-forgotten support).</summary>
    Task EraseCustomerAsync(string customerId, CancellationToken ct = default);
}

/// <summary>Input record for upserting a customer node.</summary>
public sealed record CustomerNodeInput(
    string CustomerId,
    string Email,
    string? Name,
    string Currency,
    string? City = null,
    string? Country = null,
    IReadOnlyList<string>? Tags = null);
