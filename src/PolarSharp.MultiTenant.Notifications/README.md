# PolarSharp.MultiTenant.Notifications

Opt-in tenant lifecycle notification dispatcher for PolarSharp.MultiTenant. Subscribes to `TenantStatusChangedNotification` (the MediatR notification published by `ITenantStatusService`) and sends templated email + SMS + webhook messages to the tenant's site manager.

## Install

```bash
dotnet add package PolarSharp.MultiTenant.Notifications
```

## Quickstart

```csharp
builder.Services
    .AddPolarMultiTenant()
    .AddPolarTenantLifecycle(builder.Configuration)
    .AddPolarMultiTenantNotifications(builder.Configuration);
```

```jsonc
// appsettings.json
{
  "PolarSharp": {
    "MultiTenant": {
      "Notifications": {
        "Enabled": true,
        "EnabledChannels": { "Email": true, "Sms": false, "Webhook": false },
        "Email": {
          "FromAddress": "no-reply@acme.example",
          "FromDisplayName": "Acme Platform",
          "SendGrid": { "ApiKeyEnvVar": "SENDGRID_API_KEY" }
        }
      }
    }
  }
}
```

The package is **opt-in at two levels**: you have to install the NuGet AND set `Enabled = true`. With the package installed but `Enabled` left at its default `false`, the registered MediatR handler runs but immediately returns — no validation cost, no outbound HTTP, safe to bake into a base image and flip on per environment.

## What this package does

- Subscribes to `PolarSharp.MultiTenant.Lifecycle.TenantStatusChangedNotification` via MediatR's `INotificationHandler` pattern.
- Resolves a host-configurable template for the lifecycle transition (Active->Suspended, Suspended/Inactive->Active, Active->Inactive, any->Deleted).
- Substitutes placeholders (`{TenantName}`, `{NewStatus}`, `{Reason}`, `{OccurredAt}`, etc.) and dispatches the rendered message across enabled channels in parallel.
- Three channels ship in v1.0:
  - **Email** — SendGrid v3 Mail Send API (direct HTTP, AOT-clean, no SDK dependency).
  - **SMS** — Twilio Programmable Messages API (direct HTTP, AOT-clean, no SDK dependency).
  - **Webhook** — POSTs the full notification payload as JSON with an `X-PolarSharp-Signature: sha256={hex}` HMAC header for receiver verification.
- Each channel runs in its own task; failures are logged + isolated and never propagate back through MediatR into `ITenantStatusService`.

## Credentials live in environment variables, not appsettings

Secrets — the SendGrid API key, the Twilio Account SID + Auth Token, the webhook signing secret — are referenced by **environment variable name** in appsettings, not by value. The validator logs a warning at startup if a referenced env var is unset; it never fails (secrets often arrive after process start via systemd `EnvironmentFile=`, Docker secrets, secrets managers, etc.).

## Status

v1.0.0 ships with PolarSharp v1.2.x as part of Stage C of the multi-tenant lifecycle work (see Case Study 05 — Multi-Tenancy as Optional).

## See also

- `PolarSharp.MultiTenant` — publishes the `TenantStatusChangedNotification` this package consumes.
- `docs/articles/multi-tenancy-as-optional.md` — Case Study 05 on the [GitHub Pages site](https://mollsandhersh.github.io/Polar.sh_Nuget/) for the full lifecycle + opt-in story.

## License

MIT.
