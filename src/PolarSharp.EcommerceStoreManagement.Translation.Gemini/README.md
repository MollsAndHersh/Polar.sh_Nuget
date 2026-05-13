# PolarSharp.EcommerceStoreManagement.Translation.Gemini

Google Gemini implementation of `IPolarCatalogTranslator` for PolarSharp.EcommerceStoreManagement.

## Install

```sh
dotnet add package PolarSharp.EcommerceStoreManagement.Translation.Gemini
```

## Quickstart

```csharp
builder.Services
    .AddPolarEcommerce()
    .UseSqlServerCatalog(connStr)
    .UseGeminiTranslator(opts =>
    {
        opts.ApiKey = builder.Configuration["GEMINI_API_KEY"];
        opts.Model = "gemini-2.5-flash";
    });
```

Per-tenant API keys live in `TenantBusinessProfile.TranslationApiKeyEncrypted` (encrypted at rest via the Data Protection API). The 3-tier resolver (`ITranslationProviderResolver`) prefers per-tenant credentials and falls back to this master config.
