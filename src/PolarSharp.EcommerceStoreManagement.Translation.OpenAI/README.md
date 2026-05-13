# PolarSharp.EcommerceStoreManagement.Translation.OpenAI

OpenAI implementation of `IPolarCatalogTranslator`. Auto-translates product/service descriptions on catalog save.

## Install

```bash
dotnet add package PolarSharp.EcommerceStoreManagement.Translation.OpenAI
```

## Quickstart

```csharp
builder.Services
    .AddPolarEcommerce()
    .UseSqlServer(connStr)
    .UseOpenAITranslator(opts =>
    {
        opts.ApiKey = builder.Configuration["OPENAI_API_KEY"];
        opts.Model = "gpt-4o";
    });
```

You pay OpenAI directly. See `docs/articles/catalog-translation.md`.

## License

MIT.
