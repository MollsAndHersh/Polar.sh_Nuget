# PolarSharp.EcommerceStoreManagement.Translation.Anthropic

Anthropic Claude implementation of `IPolarCatalogTranslator`. Auto-translates product/service descriptions from a master language to all supported target languages on catalog save.

## Install

```bash
dotnet add package PolarSharp.EcommerceStoreManagement.Translation.Anthropic
```

## Quickstart

```csharp
builder.Services
    .AddPolarEcommerce()
    .UseSqlServer(connStr)
    .UseAnthropicTranslator(opts =>
    {
        opts.ApiKey = builder.Configuration["ANTHROPIC_API_KEY"];
        opts.Model = "claude-sonnet-4-6";
    });
```

You pay Anthropic directly for translation calls; PolarSharp adds no markup. See `docs/articles/catalog-translation.md` on the [GitHub Pages site](https://mollsandhersh.github.io/Polar.sh_Nuget/).

## License

MIT.
