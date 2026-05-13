# PolarSharp.Onboarding

Programmatic merchant onboarding for Polar.sh. Two paths:

- **Hybrid OAuth-linking (primary)**: redirect users to Polar's consent screen; capture the access token via OAuth2 authorization code flow; register the host's webhook endpoint
- **Fully programmatic (fallback)**: POST `/v1/organizations/`, POST `/v1/organization-access-tokens/`, POST `/v1/webhooks/endpoints/` — for headless multi-tenant SaaS hosts that need to provision tenants without user interaction

## Install

```bash
dotnet add package PolarSharp.Onboarding
```

## Quickstart

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarOnboarding(opts =>
    {
        opts.OAuth.ClientId = builder.Configuration["PolarSharp:Onboarding:OAuth:ClientId"];
        opts.OAuth.ClientSecret = builder.Configuration["PolarSharp:Onboarding:OAuth:ClientSecret"];
        opts.OAuth.RedirectUri = "https://yourapp.com/onboard/callback";
    });

builder.Services.AddSingleton<IOnboardedTenantSink, EfMultiTenantStoreSink>();
```

When used with `PolarSharp.MultiTenant.Identity`, an `OnboardingPostProcessor` automatically creates a `TenantAdmin` user with a single-use password reset token on every new tenant.

See `docs/articles/onboarding.md` on the [GitHub Pages site](https://mollsandhersh.github.io/Polar.sh_Nuget/) for full OAuth flow walkthrough and programmatic provisioning examples.

## License

MIT.
