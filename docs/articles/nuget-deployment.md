# NuGet Feed Deployment

This article covers publishing PolarSharp packages to GitHub Packages and keeping the documentation site live.

## Overview

Four packages are published from this repository, all versioned together and released atomically:

| Package | ID |
|---|---|
| Core SDK | `PolarSharp` |
| Webhook integration | `PolarSharp.Webhooks` |
| Multi-tenant support | `PolarSharp.MultiTenant` |
| dotnet new templates | `PolarSharp.Templates` |

Packages are hosted on [GitHub Packages](https://github.com/mollsandhersh/Polar.sh_Nuget/packages) — no NuGet.org account required to publish. The `GITHUB_TOKEN` that GitHub Actions provides automatically is sufficient; no additional secrets need to be created.

---

## One-Time Setup

### 1. Create the GitHub repository

```bash
git remote add origin https://github.com/mollsandhersh/Polar.sh_Nuget.git
git branch -M main
git push -u origin main
```

### 2. Enable GitHub Pages

1. Go to **Settings → Pages** in the `mollsandhersh/Polar.sh_Nuget` repository.
2. Set **Source** to **GitHub Actions**.
3. Leave everything else at defaults.

The `docs.yml` workflow deploys the site on every push to `main`. URL: `https://mollsandhersh.github.io/Polar.sh_Nuget/`.

### 3. Add the Polar sandbox token secret (integration tests)

Add `POLAR_SANDBOX_TOKEN` as a GitHub Actions secret containing a valid Polar sandbox Organization Access Token. Integration tests are skipped if this secret is absent — safe for forks and offline development.

No NuGet API key or package registry credentials are needed. The `GITHUB_TOKEN` provided automatically by GitHub Actions has the `packages: write` permission the publish job needs.

---

## Publishing a Release

Publishing happens in two steps: bump the version, push a tag. GitHub Actions does the rest.

### Step 1 — Bump the version

Update the `<Version>` element in all four project files:

```bash
# Example: releasing v1.0.0
OLD=0.1.0
NEW=1.0.0

sed -i '' "s|<Version>${OLD}</Version>|<Version>${NEW}</Version>|g" \
    src/PolarSharp/PolarSharp.csproj \
    src/PolarSharp.Webhooks/PolarSharp.Webhooks.csproj \
    src/PolarSharp.MultiTenant/PolarSharp.MultiTenant.csproj \
    templates/PolarSharp.Templates/PolarSharp.Templates.csproj
```

### Step 2 — Update CHANGELOG.md

Add a section for the new version following the [Common Changelog](https://common-changelog.org/) format.

### Step 3 — Commit, tag, and push

```bash
git add src/ templates/ CHANGELOG.md
git commit -m "chore: release v1.0.0"
git push origin main

git tag v1.0.0
git push origin v1.0.0
```

That's it. The `ci.yml` `publish` job detects the `v1.0.0` tag and automatically:

1. Builds and tests the solution
2. Packs all four `.nupkg` files
3. Pushes them to [GitHub Packages](https://github.com/mollsandhersh/Polar.sh_Nuget/packages)
4. Creates a GitHub Release at `github.com/mollsandhersh/Polar.sh_Nuget/releases/tag/v1.0.0`

> **SemVer rules enforced by the public API snapshot tests:**
> - **PATCH** (`1.0.x`) — no public API changes; snapshot diffs must be empty
> - **MINOR** (`1.x.0`) — additive public API changes only; snapshot diff accepted + documented in CHANGELOG
> - **MAJOR** (`x.0.0`) — any breaking change; the snapshot diff is the migration guide

### Version alignment

All four packages are versioned identically — consumers install them as a coherent set. Never release `PolarSharp.Webhooks 1.1.0` against `PolarSharp 1.0.0`.

---

## Installing Packages (Consumer Setup)

Because GitHub Packages requires authentication even for public packages, consumers add the feed to their `NuGet.config`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="PolarSharp" value="https://nuget.pkg.github.com/mollsandhersh/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <PolarSharp>
      <add key="Username" value="GITHUB_USERNAME" />
      <add key="ClearTextPassword" value="GITHUB_PAT" />
    </PolarSharp>
  </packageSourceCredentials>
</configuration>
```

The `GITHUB_PAT` is a [Personal Access Token](https://github.com/settings/tokens) with the `read:packages` scope. A read-only token is sufficient.

For CI/CD in consumer projects, store the PAT as a secret and pass it via environment variable or `dotnet nuget add source`.

---

## Manual Publishing (Emergency / Local)

If CI is unavailable, packages can be pushed locally:

```bash
# Pack all four
dotnet pack src/PolarSharp -c Release -o artifacts/
dotnet pack src/PolarSharp.Webhooks -c Release -o artifacts/
dotnet pack src/PolarSharp.MultiTenant -c Release -o artifacts/
dotnet pack templates/PolarSharp.Templates -c Release -o artifacts/

# Push to GitHub Packages (requires a PAT with packages:write scope)
dotnet nuget push "artifacts/*.nupkg" \
    --api-key $GITHUB_TOKEN \
    --source "https://nuget.pkg.github.com/mollsandhersh/index.json" \
    --skip-duplicate
```

`--skip-duplicate` makes the command idempotent — safe to re-run if a version was already pushed.

---

## Optional: NuGet Package Signing

Code-signed NuGet packages prove provenance and satisfy enterprise security audits. To enable:

1. Obtain a code-signing certificate (DigiCert, Sectigo, or SignPath cloud — annual cost ~$200–400).
2. Export as a `.pfx` file and base64-encode it: `base64 -i cert.pfx | pbcopy`
3. Add GitHub Actions secrets:
   - `CODE_SIGN_CERT_BASE64` — the base64-encoded `.pfx`
   - `CODE_SIGN_CERT_PASSWORD` — the `.pfx` password
4. Add a signing step to the `publish` job in `.github/workflows/ci.yml` before the push step:

```yaml
- name: Sign NuGet packages
  run: |
    echo "$CODE_SIGN_CERT_BASE64" | base64 -d > cert.pfx
    for pkg in artifacts/*.nupkg; do
      dotnet nuget sign "$pkg" \
        --certificate-path cert.pfx \
        --certificate-password "$CODE_SIGN_CERT_PASSWORD" \
        --timestamper http://timestamp.digicert.com
    done
  env:
    CODE_SIGN_CERT_BASE64: ${{ secrets.CODE_SIGN_CERT_BASE64 }}
    CODE_SIGN_CERT_PASSWORD: ${{ secrets.CODE_SIGN_CERT_PASSWORD }}
```

Note: Assemblies are already strong-named via `PolarSharp.snk` (committed public key). Strong-naming and NuGet package signing are independent — strong-naming is already active without a code-signing certificate.

---

## Verifying a Published Release

After the tag is pushed and CI completes (~3–5 minutes):

1. **GitHub Packages listing:**
   ```
   https://github.com/mollsandhersh/Polar.sh_Nuget/packages
   ```
   All four packages appear with the released version number.

2. **GitHub Release:**
   ```
   https://github.com/mollsandhersh/Polar.sh_Nuget/releases
   ```
   The release is created automatically with the `.nupkg` files attached.

3. **Install in a test project:**
   ```bash
   dotnet new web -n PolarSharpSmokeTest
   dotnet add PolarSharpSmokeTest package PolarSharp
   dotnet build PolarSharpSmokeTest
   ```

4. **Verify package contents** (README visible, XML docs present):
   ```bash
   # .nupkg is a zip file — inspect its contents
   unzip -l artifacts/PolarSharp.1.0.0.nupkg | grep -E "README|\.xml$|\.dll$"
   ```
   Expected: `README.md`, `lib/net10.0/PolarSharp.dll`, `lib/net10.0/PolarSharp.xml`.

---

## Rollback

To pull a broken GitHub Packages version:

1. Go to the package page → **Manage versions** → delete the broken version.
2. Delete the corresponding GitHub Release and tag.
3. Publish a corrected patch release (`x.x.1`) immediately.
4. Document the issue in `CHANGELOG.md` under the patch version.

---

## Documentation Website

The documentation site at `https://mollsandhersh.github.io/Polar.sh_Nuget/` is built and deployed by the `docs.yml` workflow on every push to `main`. To preview locally:

```bash
# Install DocFX (one-time)
dotnet tool install -g docfx

# Build the solution first (generates XML doc comment files)
dotnet build --configuration Release

# Build and serve
docfx docfx.json
docfx serve docs/_site    # browse at http://localhost:8080
```

To add a new article:
1. Create `docs/articles/my-topic.md`
2. Add it to `docs/articles/toc.yml`
3. Push to `main` — the site redeploys automatically
