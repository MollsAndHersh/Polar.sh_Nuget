namespace PolarSharp.EcommerceStoreManagement.Translation;

/// <summary>
/// Reads the current tenant's translation configuration from the underlying persistence
/// store. Abstracted out of <see cref="ITranslationProviderResolver"/> so the resolver
/// can be unit-tested without an EF Core DbContext or a Finbuckle multi-tenant scope.
/// </summary>
/// <remarks>
/// The default implementation lives in <c>PolarSharp.EcommerceStoreManagement.EntityFrameworkCore</c>
/// and queries <c>TenantBusinessProfileEntity</c> via the catalog DbContext, relying on
/// the existing global tenant query filter for scoping. Hosts using a non-EF persistence
/// model can supply their own implementation.
/// </remarks>
public interface ITenantTranslationConfigLookup
{
    /// <summary>
    /// Returns the current tenant's translation configuration, or <see langword="null"/>
    /// when no profile row exists or no current tenant context is in scope.
    /// </summary>
    Task<TenantTranslationConfig?> GetAsync(CancellationToken ct = default);
}

/// <summary>
/// Translation-specific projection of <c>TenantBusinessProfile</c> — only the fields
/// <see cref="ITranslationProviderResolver"/> needs. Encrypted API key is passed through
/// unchanged; decryption is the resolver's responsibility.
/// </summary>
/// <param name="TenantId">The current tenant's identifier. Snapshotted for logging only.</param>
/// <param name="Provider">The configured translation provider. <see cref="TranslationProvider.None"/> means "fall through to master".</param>
/// <param name="EncryptedApiKey">Ciphertext API key produced by ASP.NET Core Data Protection. <see langword="null"/> when never configured.</param>
/// <param name="Model">Provider-specific model name (e.g. <c>"claude-sonnet-4-6"</c>, <c>"gpt-4o"</c>).</param>
/// <param name="Endpoint">Optional endpoint override (Azure OpenAI deployment URL, Grok base URL, etc.).</param>
public sealed record TenantTranslationConfig(
    string TenantId,
    TranslationProvider Provider,
    string? EncryptedApiKey,
    string? Model,
    string? Endpoint);
