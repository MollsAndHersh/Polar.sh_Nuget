# Getting Started with PolarSharp

PolarSharp is a .NET 10 AOT-compatible SDK for [Polar.sh](https://polar.sh), an open-source Merchant of Record payment platform.

## Installation

```bash
# Core library (required)
dotnet add package PolarSharp

# Optional: webhook handler registration + signature verification
dotnet add package PolarSharp.Webhooks

# Optional: multi-tenant support (Finbuckle.MultiTenant integration)
dotnet add package PolarSharp.MultiTenant
```

## Configuration

Add a `PolarSharp` section to your `appsettings.json`:

```json
{
  "PolarSharp": {
    "Mode": "Test",
    "AccessToken": "tok_sandbox_xxx"
  }
}
```

Set `Mode` to `"Test"` for Polar's sandbox (no real charges) or `"Live"` for production.

Use [user-secrets](https://learn.microsoft.com/dotnet/user-secrets) to keep the token out of source control:

```bash
dotnet user-secrets set "PolarSharp:AccessToken" "tok_sandbox_xxx"
```

## Minimal registration (`Program.cs`)

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register PolarSharp services
builder.Services
    .AddPolarInfrastructure(builder.Configuration)   // core
    .AddPolarWebhooks()                               // optional
    .AddPolarMultiTenant();                           // optional

var app = builder.Build();

// UseRequestLocalization before UsePolarInfrastructure (sets culture for error messages)
app.UseRequestLocalization(opts =>
    opts.SetDefaultCulture("en-US")
        .AddSupportedUICultures("en-US", "es-MX"));

// Wire middleware: UseMultiTenancy + MapPolarWebhooks — one call handles all
app.UsePolarInfrastructure();

// Map your own endpoints
app.MapGet("/orders/{id}", async (string id, PolarClient polar, CancellationToken ct) =>
    (await polar.Orders[id].GetAsync(cancellationToken: ct))
        .Match(
            onSuccess: order => Results.Ok(order),
            onFailure: error => error.ToHttpResult()));

app.Run();
```

See [Middleware Pipeline Ordering](pipeline-ordering.md) for the correct position of `UsePolarInfrastructure()`.

## First API call

Inject `PolarClient` into any handler, controller, or service:

```csharp
app.MapGet("/products", async (PolarClient polar, CancellationToken ct) =>
{
    var result = await polar.Products.EmptyPathSegment.GetAsync(cancellationToken: ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
});
```

Resource methods return `Task<Result<TValue, PolarError>>` — never throw for HTTP 4xx responses.
Pattern-match the result with `.Match(onSuccess, onFailure)`.

## Next steps

- [Local Development Setup](local-development.md) — sandbox token, user-secrets, ngrok tunnel, webhook testing
- [Configuration reference](configuration.md) — all `appsettings.json` options
- [Test vs. Live Mode](test-vs-live-mode.md) — sandbox vs. production
- [Webhooks](webhooks.md) — receive and verify Polar events
- [API Versioning](api-versioning.md) — pin to a Polar schema version
- [Security](security.md) — webhook hardening checklist
