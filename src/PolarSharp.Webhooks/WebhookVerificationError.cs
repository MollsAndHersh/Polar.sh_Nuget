namespace PolarSharp.Webhooks;

/// <summary>
/// Represents a webhook signature verification failure.
/// </summary>
/// <remarks>
/// <para>
/// Returned by <see cref="WebhookValidator.Verify"/> as the failure value of a
/// <see cref="Result{TValue,TError}"/> when HMAC verification or timestamp validation fails.
/// Never thrown as an exception.
/// </para>
/// <para>
/// The <see cref="Message"/> is intentionally opaque — it does not reveal whether the
/// failure was caused by a bad signature, an expired timestamp, or a missing header.
/// The specific failure reason is logged internally at <c>Warning</c> level. This prevents
/// timing and information oracle attacks that could help an attacker distinguish failure modes.
/// </para>
/// </remarks>
/// <param name="Message">
/// An opaque, caller-safe error description. Always the same string regardless of the
/// specific failure cause, to prevent information leakage.
/// </param>
public sealed record WebhookVerificationError(string Message);
