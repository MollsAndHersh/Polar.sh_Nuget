# Localization

PolarSharp ships built-in localization for `en-US` (default) and `es-MX`. All user-facing error messages, webhook errors, and toast notification strings are fully translated in both cultures out of the box — zero configuration required.

## How it works

PolarSharp resolves strings via `IPolarLocalizer`, which wraps `IStringLocalizer<PolarMessages>` backed by embedded `.resx` satellite assemblies.

`CultureInfo.CurrentUICulture` at the time of each string lookup determines which language is returned. ASP.NET Core's `UseRequestLocalization` middleware sets this from the `Accept-Language` header (or query string / route).

## Enabling additional cultures

In `Program.cs`:

```csharp
app.UseRequestLocalization(opts =>
    opts.SetDefaultCulture("en-US")
        .AddSupportedUICultures("en-US", "es-MX", "fr-FR"));
```

PolarSharp will use `en-US` as the fallback for `fr-FR` until French `.resx` files are added.

## Custom localizer

To replace the built-in strings entirely (database-backed, translation API, etc.), register your own `IPolarLocalizer` **before** calling `AddPolarInfrastructure`:

```csharp
services.AddSingleton<IPolarLocalizer, MyDatabaseLocalizer>();
services.AddPolarInfrastructure(builder.Configuration);
```

The `TryAddSingleton` pattern ensures the built-in implementation is not registered when a custom one already exists.

## Adding a new language

1. Copy `src/PolarSharp/Localization/Resources/PolarMessages.resx` to `PolarMessages.fr-FR.resx`
2. Translate all values (keys must be identical — see `PolarMessageKeys.cs` for the full list)
3. Do the same for `src/PolarSharp.Webhooks/Localization/Resources/PolarWebhookMessages.fr-FR.resx`
4. Run the CI test `AllMessageKeys_PresentIn_Resx("fr-FR")` — it will fail until all keys are translated

The CI test catches any key added to `PolarMessageKeys.cs` that is missing from any supported `.resx` file.

## Available localization keys

All keys are defined in `PolarMessageKeys` (core) and `PolarWebhookMessageKeys` (webhooks). Examples:

| Key | en-US | es-MX |
|-----|-------|-------|
| `Error_Unauthorized` | Authentication failed. Verify your Polar access token. | Error de autenticación. Verifique su token de acceso de Polar. |
| `Error_NotFound` | The requested resource was not found. | El recurso solicitado no fue encontrado. |
| `Webhook_SignatureInvalid` | Webhook signature verification failed. | La verificación de firma del webhook falló. |
| `Toast_order_created_Title` | New Order | Nuevo Pedido |

## Testing localization

Use `GET /test/localization/errors?culture=es-MX` in PolarTestApp to verify that a given culture returns the correct translations.
