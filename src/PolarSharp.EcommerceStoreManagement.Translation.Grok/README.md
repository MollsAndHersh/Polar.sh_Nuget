# PolarSharp.EcommerceStoreManagement.Translation.Grok

xAI Grok implementation of `IPolarCatalogTranslator` for PolarSharp.EcommerceStoreManagement.

## Install

```sh
dotnet add package PolarSharp.EcommerceStoreManagement.Translation.Grok
```

## Quickstart

```csharp
builder.Services
    .AddPolarEcommerce()
    .UseSqlServerCatalog(connStr)
    .UseGrokTranslator(opts =>
    {
        opts.ApiKey = builder.Configuration["XAI_API_KEY"];
        opts.Model = "grok-4-fast";
    });
```

Grok uses xAI's OpenAI-compatible REST endpoint (default: `https://api.x.ai/v1`). The package uses raw HttpClient + JSON, so the same code works against any drop-in OpenAI-compatible endpoint by overriding `opts.Endpoint`.
