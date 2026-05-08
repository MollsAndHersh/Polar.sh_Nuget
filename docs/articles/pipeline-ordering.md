# Middleware Pipeline Ordering

`app.UsePolarInfrastructure()` must be placed at a specific position in your ASP.NET Core middleware pipeline. Placing it too early or too late breaks certain features.

## Correct order

```csharp
app.UseExceptionHandler();          // or UseDeveloperExceptionPage()
app.UseHsts();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();                   // ← MUST come before UsePolarInfrastructure
app.UseCors();
// ─────────────────────────────────────────────────────────
app.UsePolarInfrastructure();       // ← INSERT HERE
// ─────────────────────────────────────────────────────────
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();               // or MapGet/MapHub/etc.
```

## What `UsePolarInfrastructure` does internally

In the order shown, it inserts:

1. **`UseRequestLocalization`** — sets `CultureInfo.CurrentUICulture` so PolarSharp error messages come back in the correct language. Skipped if the host has already called `UseRequestLocalization`.
2. **`UseMultiTenant`** (Finbuckle) — resolves the current tenant from the request. Requires `UseRouting` to have run first (route-based strategy reads route data). Skipped if `AddPolarMultiTenant` was not called.
3. **`MapPolarWebhooks`** — registers the `POST {path}` webhook receiver route. Skipped if `AddPolarWebhooks` was not called.

## Why before `UseAuthentication`?

Placing `UsePolarInfrastructure` before `UseAuthentication` ensures that:

- Request localization is active when auth middleware emits error responses (so 401 messages can be localized).
- Tenant is resolved before auth, which is required for per-tenant auth schemes.

## Minimal API (no explicit `UseRouting`)

In Minimal API apps (the default `dotnet new web` template), `UseRouting` is implicit. `UsePolarInfrastructure` works correctly without an explicit `UseRouting` call. The internal `MapPolarWebhooks` call triggers endpoint routing correctly.

## Common mistakes

| Mistake | Symptom | Fix |
|---|---|---|
| `UsePolarInfrastructure` before `UseRouting` | Route-based tenant strategy fails — tenant is always null | Move `UseRouting()` earlier |
| `UsePolarInfrastructure` after `UseAuthentication` | Localized error messages always in `en-US` even for non-English users | Move `UsePolarInfrastructure` before `UseAuthentication` |
| `UsePolarInfrastructure` after `MapControllers` | Webhook route not registered — Polar gets 404 | Move `UsePolarInfrastructure` before endpoint mapping |
| Calling `UseMultiTenant()` separately | Double middleware insertion — may cause duplicate tenant resolution | Remove manual `UseMultiTenant()` call; `UsePolarInfrastructure` handles it |

## Complete `Program.cs` template

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarWebhooks()
    .AddPolarMultiTenant();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();
else
    app.UseExceptionHandler("/error");

app.UseHttpsRedirection();
app.UseStaticFiles();

// UsePolarInfrastructure BEFORE UseAuthentication
app.UseRequestLocalization(opts =>
    opts.SetDefaultCulture("en-US")
        .AddSupportedUICultures("en-US", "es-MX"));

app.UsePolarInfrastructure();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapControllers();
// or:
app.MapGet("/...", ...);

app.Run();
```
