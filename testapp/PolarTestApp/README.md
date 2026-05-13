# PolarTestApp

Reference test/demo application that exercises the full PolarSharp surface (core SDK + webhooks + multi-tenant). Used by the AOT-publish smoke test in CI and as the local sandbox for manually exercising new features against a live Polar.sh sandbox.

```bash
dotnet run --project testapp/PolarTestApp
# Scalar API explorer:    http://localhost:5xxx/scalar
# Webhook endpoint:       POST http://localhost:5xxx/hooks/polar
```

## Polar sandbox token (site-master, NOT per-tenant)

The Polar Organization Access Token (OAT) used by **the SaaS site itself** — for cross-cutting operations like creating tenants programmatically, monitoring health, etc. — is **not** stored in `appsettings.json`. The placeholder `"PolarSharp:AccessToken": ""` in `appsettings.json` is intentionally empty.

Three ways to provide the token, in increasing priority:

1. **dotnet user-secrets (local dev, Development environment)** — the project ships with `<UserSecretsId>PolarTestApp-secrets</UserSecretsId>`. Set the value once and it persists in `~/.microsoft/usersecrets/PolarTestApp-secrets/secrets.json` outside the repo:

   ```bash
   dotnet user-secrets set "PolarSharp:AccessToken" "polar_oat_..." --project testapp/PolarTestApp
   ```

2. **`POLAR_SANDBOX_TOKEN` environment variable (recommended — same pattern works locally and in CI)** — `Program.cs` reads this env var and binds it to `PolarSharp:AccessToken` at startup. Locally it's loaded from `.env` via direnv (see the repo root `.env.example` and `.envrc`); in CI it's injected from the GitHub Actions secret of the same name.

   ```bash
   # .env at the repo root (gitignored):
   POLAR_SANDBOX_TOKEN=polar_oat_...
   ```

3. **Direct in appsettings.Development.json (least preferred)** — works for one-off testing but DON'T commit a real token; `appsettings.Development.json` is tracked.

### Per-tenant tokens

Each tenant in `PolarSharp:MultiTenant:Tenants[N]` has its own `PolarAccessToken` field — those are tenant-scoped credentials, completely separate from the site-master token above. The placeholder strings (`""`) in `appsettings.json` are intentional. Real per-tenant tokens come from:

- The host's tenant store (when `PolarSharp.MultiTenant.EntityFrameworkCore` is wired)
- Per-tenant user-secrets entries (e.g. `dotnet user-secrets set "PolarSharp:MultiTenant:Tenants:0:PolarAccessToken" "polar_oat_..."`)
- An out-of-band onboarding flow (`PolarSharp.Onboarding`) that captures each tenant's OAT during signup

## Webhook signing secret

`PolarSharp:Webhooks:Secret` and each tenant's `WebhookSecret` follow the same hardcode-free convention. The placeholder `whsec_..._replace_via_user_secrets` makes the intent explicit. For local dev:

```bash
dotnet user-secrets set "PolarSharp:Webhooks:Secret" "whsec_..." --project testapp/PolarTestApp
```
