# NuGet Feed Deployment

This article covers everything needed to publish PolarSharp packages to NuGet.org and to keep the documentation website live. It also describes the one-time GitHub setup steps that activate both pipelines.

## Overview

Three packages are published from this repository:

| Package | NuGet ID |
|---|---|
| Core SDK | `PolarSharp` |
| Webhook integration | `PolarSharp.Webhooks` |
| Multi-tenant support | `PolarSharp.MultiTenant` |

All three are versioned together and published atomically via a CI workflow triggered by a git tag.

---

## One-Time Setup

### 1. Create the GitHub repository

```bash
# From the repo root
git remote add origin https://github.com/mollsandhersh/Polar.sh_Nuget.git
git branch -M main
git push -u origin main
```

The `RepositoryUrl` in all three library `.csproj` files already points to `https://github.com/mollsandhersh/Polar.sh_Nuget`, so SourceLink will resolve correctly after this push.

### 2. Enable GitHub Pages

1. Go to **Settings → Pages** in the `mollsandhersh/Polar.sh_Nuget` repository.
2. Set **Source** to **GitHub Actions**.
3. Leave everything else at defaults.

The `docs.yml` workflow will deploy the site on the very next push to `main`. The URL will be `https://mollsandhersh.github.io/Polar.sh_Nuget/`.

### 3. Add the NuGet API key secret

1. Sign in to [nuget.org](https://www.nuget.org) and go to **API keys**.
2. Create a key scoped to **Push new packages and package versions** for the `PolarSharp*` glob.
3. In the GitHub repo, go to **Settings → Secrets and variables → Actions**.
4. Add a secret named `NUGET_API_KEY` containing the key value.

The `ci.yml` publish job reads `${{ secrets.NUGET_API_KEY }}` — no further config changes needed.

### 4. Add the Polar sandbox token secret (integration tests)

Add `POLAR_SANDBOX_TOKEN` as a GitHub Actions secret containing a valid Polar sandbox Organization Access Token. Integration tests are skipped if this secret is absent (safe for forks and offline development); they run automatically on pushes to `main`.

### 5. Optional: NuGet package signing

Code-signed NuGet packages prove provenance and satisfy enterprise security audits. To enable:

1. Obtain a code-signing certificate (DigiCert, Sectigo, or SignPath cloud — annual cost ~$200–400).
2. Export as a `.pfx` file and base64-encode it: `base64 -i cert.pfx | pbcopy`
3. Add GitHub Actions secrets:
   - `CODE_SIGN_CERT_BASE64` — the base64-encoded `.pfx`
   - `CODE_SIGN_CERT_PASSWORD` — the `.pfx` password
4. Add a signing step to the `publish` job in `.github/workflows/ci.yml`:

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

Note: Assemblies are already strong-named via `PolarSharp.snk` (committed public key; private key held in this secret). Strong-naming and NuGet package signing are independent — strong-naming is already active without a code-signing certificate.

---

## Publishing a Release

### Standard release flow

1. **Bump the version** in all three library `.csproj` files and `PolarSharp.Templates`:

   ```bash
   # Example: bump from 1.0.0 to 1.1.0
   sed -i '' 's|<Version>1.0.0</Version>|<Version>1.1.0</Version>|g' \
       src/PolarSharp/PolarSharp.csproj \
       src/PolarSharp.Webhooks/PolarSharp.Webhooks.csproj \
       src/PolarSharp.MultiTenant/PolarSharp.MultiTenant.csproj \
       templates/PolarSharp.Templates/PolarSharp.Templates.csproj
   ```

2. **Update `CHANGELOG.md`** — add a section for the new version following the [Common Changelog](https://common-changelog.org/) format.

3. **Commit and push:**

   ```bash
   git add -A
   git commit -m "chore: release v1.1.0"
   git push origin main
   ```

4. **Create and push the release tag:**

   ```bash
   git tag v1.1.0
   git push origin v1.1.0
   ```

The `ci.yml` `publish` job triggers on `refs/tags/v*`. It runs the full build + test + AOT smoke test, then packs and pushes all three packages to NuGet.org. A GitHub Release is created automatically with the CHANGELOG.md section for this version as the release notes.

> **SemVer rules enforced by the public API snapshot tests:**
> - **PATCH** (`1.0.x`) — no public API changes; snapshot diffs must be empty
> - **MINOR** (`1.x.0`) — additive public API changes only; snapshot diff accepted + documented in CHANGELOG
> - **MAJOR** (`x.0.0`) — any breaking change; the snapshot diff is the migration guide

### Version alignment

All three packages are versioned identically — consumers install them as a coherent set. Never release `PolarSharp.Webhooks 1.1.0` against `PolarSharp 1.0.0`.

---

## Manual Publishing (Emergency / Local)

If CI is unavailable, packages can be pushed locally:

```bash
# Pack
dotnet pack src/PolarSharp -c Release -o artifacts/
dotnet pack src/PolarSharp.Webhooks -c Release -o artifacts/
dotnet pack src/PolarSharp.MultiTenant -c Release -o artifacts/

# Push (requires NUGET_API_KEY in environment or ~/.nuget/NuGet/NuGet.Config)
dotnet nuget push artifacts/PolarSharp.*.nupkg \
    --api-key $NUGET_API_KEY \
    --source https://api.nuget.org/v3/index.json \
    --skip-duplicate

dotnet nuget push artifacts/PolarSharp.Webhooks.*.nupkg \
    --api-key $NUGET_API_KEY \
    --source https://api.nuget.org/v3/index.json \
    --skip-duplicate

dotnet nuget push artifacts/PolarSharp.MultiTenant.*.nupkg \
    --api-key $NUGET_API_KEY \
    --source https://api.nuget.org/v3/index.json \
    --skip-duplicate
```

`--skip-duplicate` prevents failure if a version was already published (idempotent).

---

## GitHub Packages (Private / Internal Feed)

If your organization needs a private pre-release feed (e.g., for internal beta testing before NuGet.org publication), GitHub Packages provides one at no additional cost.

```bash
# Authenticate
dotnet nuget add source \
    "https://nuget.pkg.github.com/mollsandhersh/index.json" \
    --name "PolarSharp-GitHub" \
    --username mollsandhersh \
    --password $GITHUB_TOKEN \
    --store-password-in-clear-text

# Push
dotnet nuget push artifacts/PolarSharp.*.nupkg \
    --api-key $GITHUB_TOKEN \
    --source "PolarSharp-GitHub"
```

Consumers install from GitHub Packages by adding the feed in their `NuGet.config`:

```xml
<configuration>
  <packageSources>
    <add key="PolarSharp-GitHub"
         value="https://nuget.pkg.github.com/mollsandhersh/index.json" />
  </packageSources>
</configuration>
```

---

## Verifying a Published Package

After a release tag is pushed and CI completes (~3–5 minutes):

1. **NuGet.org listing:**
   ```
   https://www.nuget.org/packages/PolarSharp/
   https://www.nuget.org/packages/PolarSharp.Webhooks/
   https://www.nuget.org/packages/PolarSharp.MultiTenant/
   ```

2. **Install in a test project:**
   ```bash
   dotnet new web -n PolarSharpSmokeTest
   dotnet add PolarSharpSmokeTest package PolarSharp
   dotnet build PolarSharpSmokeTest
   ```

3. **Verify package contents** (README visible, XML docs present, strong-naming):
   ```bash
   # Inspect the .nupkg (it's a zip file)
   unzip -l artifacts/PolarSharp.1.1.0.nupkg | grep -E "README|\.xml$|\.dll$"
   ```
   Expected output includes `README.md`, `lib/net10.0/PolarSharp.dll`, and `lib/net10.0/PolarSharp.xml`.

4. **Verify strong-naming:**
   ```bash
   # On .NET SDK — check the assembly is signed
   dotnet-sn -v artifacts/PolarSharp.dll 2>/dev/null || \
   sn -v ~/.nuget/packages/polarsharp/1.1.0/lib/net10.0/PolarSharp.dll
   ```

---

## Documentation Website

The documentation site at `https://mollsandhersh.github.io/Polar.sh_Nuget/` is built and deployed by the `docs.yml` workflow on every push to `main`. To preview locally:

```bash
# Install DocFX (one-time)
dotnet tool install -g docfx

# Build the solution first (generates XML doc comment files)
dotnet build --configuration Release

# Build the docs site
docfx build docfx.json

# Serve locally at http://localhost:8080
docfx serve docs/_site
```

> **How articles become web pages:** Every `.md` file in `docs/articles/` is automatically converted to a styled `.html` page by DocFX. The `.html` files are build artifacts in `docs/_site/` (gitignored) — never commit them. The `.md` files are the single canonical source.

To add a new article:
1. Create `docs/articles/my-topic.md`
2. Add it to `docs/articles/toc.yml`
3. Push to `main` — the site redeploys automatically

---

## Rollback

NuGet.org does not allow deleting published versions. To pull a broken release:

1. Go to `https://www.nuget.org/packages/PolarSharp/{version}` → **Delete** (unlists from search; existing installs still work).
2. Publish a patch release (`x.x.1`) with the fix immediately.
3. Document the issue in `CHANGELOG.md` under the patch version.

GitHub Releases can be deleted freely — delete the broken tag's release, push a corrected tag.
