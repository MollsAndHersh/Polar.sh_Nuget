namespace PolarSharp.Localization;

/// <summary>
/// Defines all localization key constants used by PolarSharp.
/// Every key in this class must be present in every supported <c>.resx</c> resource file.
/// </summary>
/// <remarks>
/// The CI completeness test in <c>PolarSharp.Tests</c> verifies that <c>PolarMessages.resx</c>
/// and <c>PolarMessages.es-MX.resx</c> contain every key declared here.
/// Adding a key without updating the <c>.resx</c> files will fail the build.
/// </remarks>
internal static class PolarMessageKeys
{
    // ── Core API errors ─────────────────────────────────────────────────────────
    /// <summary>HTTP 401 — authentication failure message.</summary>
    public const string Error_Unauthorized = nameof(Error_Unauthorized);

    /// <summary>HTTP 403 — authorization failure message.</summary>
    public const string Error_Forbidden = nameof(Error_Forbidden);

    /// <summary>HTTP 404 — resource not found message.</summary>
    public const string Error_NotFound = nameof(Error_NotFound);

    /// <summary>HTTP 429 — rate limit exceeded message. Argument {0} = retry-after seconds.</summary>
    public const string Error_RateLimit = nameof(Error_RateLimit);

    /// <summary>HTTP 422 — validation failure message. Argument {0} = field summary.</summary>
    public const string Error_Validation = nameof(Error_Validation);

    /// <summary>HTTP 5xx — server error message.</summary>
    public const string Error_ServerError = nameof(Error_ServerError);

    /// <summary>Network timeout message.</summary>
    public const string Error_Timeout = nameof(Error_Timeout);

    /// <summary>Network connectivity failure message.</summary>
    public const string Error_NetworkFailure = nameof(Error_NetworkFailure);

    // ── Webhook errors (PolarSharp.Webhooks) ────────────────────────────────────
    /// <summary>Webhook HMAC signature mismatch message.</summary>
    public const string Webhook_SignatureInvalid = nameof(Webhook_SignatureInvalid);

    /// <summary>Webhook timestamp outside tolerance window message.</summary>
    public const string Webhook_TimestampExpired = nameof(Webhook_TimestampExpired);

    /// <summary>Unknown webhook event type message. Argument {0} = type string.</summary>
    public const string Webhook_UnknownEventType = nameof(Webhook_UnknownEventType);

    // ── Multi-tenant errors (PolarSharp.MultiTenant) ───────────────────────────
    /// <summary>Tenant could not be resolved from the request context.</summary>
    public const string Tenant_NotResolved = nameof(Tenant_NotResolved);

    /// <summary>No Polar configuration found for the resolved tenant. Argument {0} = tenant ID.</summary>
    public const string Tenant_NotConfigured = nameof(Tenant_NotConfigured);

    /// <summary>No active multi-tenant context available.</summary>
    public const string Tenant_NoActiveContext = nameof(Tenant_NoActiveContext);
}
