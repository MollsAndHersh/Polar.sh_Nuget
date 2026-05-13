# PolarSharp.EcommerceStoreManagement.Translation.AzureOpenAI

Azure OpenAI implementation of `IPolarCatalogTranslator` — uses your Azure OpenAI deployment for description translation.

## Install

```bash
dotnet add package PolarSharp.EcommerceStoreManagement.Translation.AzureOpenAI
```

## Quickstart

```csharp
builder.Services
    .AddPolarEcommerce()
    .UseSqlServer(connStr)
    .UseAzureOpenAITranslator(opts =>
    {
        opts.Endpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"];
        opts.ApiKey = builder.Configuration["AZURE_OPENAI_KEY"];
        opts.DeploymentName = "gpt-4o";
    });
```

You pay Azure directly. See `docs/articles/catalog-translation.md`.

## License

MIT.
