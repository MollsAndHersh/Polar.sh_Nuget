# PolarSharp.PrepaidWallets — Lift-and-Shift Procedure

This document captures the complete strategy and mechanical steps for relocating the
PrepaidWallets feature out of the PolarSharp monorepo into its own standalone GitHub
repository at a future date — without rewriting any of the wallet code itself, and
without breaking any host that consumes PolarSharp's PrepaidWallets bridges.

> **Status**: Strategy authored 2026-05-15 alongside the v1.3.0 expansion plan. The
> lift-and-shift contract is the spine of the entire PrepaidWallets package
> decomposition; every package boundary, namespace, and dependency rule below was
> chosen specifically to make this lift mechanical, not architectural.

---

## The contract in one sentence

The `.Polar.` infix in the namespace `PolarSharp.PrepaidWallets.Polar.*` is the
entire lift boundary. Files with `.Polar.` in their namespace **stay** with PolarSharp
and get rewired post-lift; files without `.Polar.` (i.e. `PolarSharp.PrepaidWallets.*`
direct) **move** to the new repo via a single namespace rename.

---

## The two tiers

| Tier | Namespace pattern | Lift action | Allowed dependencies | Packages |
|---|---|---|---|---|
| **Wallet core** | `PolarSharp.PrepaidWallets.*` (no `.Polar.` infix anywhere in the name) | **Moves** to a new `PrepaidWallets/*` repo. Single namespace rename: `PolarSharp.PrepaidWallets` → `PrepaidWallets`. | MediatR, EF Core, Marten, HotChocolate, Microsoft.Extensions.*. **Zero** PolarSharp.* refs. | 10 packages: Abstractions, PrepaidWallets, EventStore.Marten, EventStore.EntityFrameworkCore (+ 5 provider variants: SqlServer/Sqlite/PostgreSQL/MariaDb/CosmosDb), Reporting |
| **Polar bridges** | `PolarSharp.PrepaidWallets.Polar.*` | **Stays** with PolarSharp. After lift, `<ProjectReference>` swaps to `<PackageReference Include="PrepaidWallets.X">` against the new external NuGet feed. | Wallet core (or external lib post-lift) + PolarSharp internals (ICurrentUser, IPolarReportingClient, ICustomerTransactionContext, etc.) | 4 packages: Polar.Identity, Polar.Checkout, Polar.Reporting, Polar.GraphQL |

---

## The mechanical CI guard makes the boundary unbreakable

Phase 22 of the v1.3.0 plan ships
`scripts/verify-prepaidwallets-no-polarsharp-deps.sh`. It runs
`dotnet list package --include-transitive` against every wallet-core csproj and
**fails the build** if any `PolarSharp.*` (other than another wallet-core package)
appears in the dependency graph. CI runs it on every PR. A developer who accidentally
adds `using PolarSharp.BaseEntities;` inside a wallet-core file breaks the build.

The guard is the entire reason the lift remains feasible — without it, the boundary
erodes silently over time.

---

## The interface seams that let bridges plug in PolarSharp behavior

Three interfaces in `PolarSharp.PrepaidWallets.Abstractions` define the integration
points. The wallet core depends only on these interfaces; the `.Polar.*` bridge
packages provide PolarSharp-specific implementations.

| Interface | Wallet-core role | Bridge implementation (pre-lift) | Post-lift action |
|---|---|---|---|
| `IWalletIdentityProvider` | "Who is performing this op?" Wallet core calls it on every command. | `PolarSharp.PrepaidWallets.Polar.Identity` wires it via PolarSharp's `ICurrentUser` + `ICurrentTenant`. | Bridge package keeps the same impl; the wallet-core interface was lifted, the bridge is rewired to depend on the new external `PrepaidWallets.Abstractions` package. |
| `IWalletTransactionContext` | Optional IP / UA / session metadata for fraud detection on every event. | `PolarSharp.PrepaidWallets.Polar.Identity` adapts v1.3 `CustomerTransactionContext` from PolarSharp.BaseEntities into the wallet-core type. | Bridge package keeps adapting; new repo's interface is identical (lifted verbatim). |
| `ITokenExchangeRateResolver` | Currency → tokens-per-unit lookup. Default impl in wallet core; bridges can override per-tenant. | `PolarSharp.PrepaidWallets.Polar.Checkout` overrides per tenant via `TenantBusinessProfile`. | Bridge package overrides the same way; default impl in lifted wallet core still works. |

Because the wallet core defines its OWN `IWalletTransactionContext` type (no
PolarSharp.BaseEntities dep), the wallet core can ship to a new repo without dragging
PolarSharp dependencies. The bridge package does the type adaptation between
PolarSharp's `CustomerTransactionContext` and the wallet's `IWalletTransactionContext`.

---

## The 5-line lift script

When you decide to lift, here's exactly what happens:

```bash
# In a new PrepaidWallets repo:
git clone the-new-repo && cd the-new-repo
cp -r ../PolarSharp/src/PolarSharp.PrepaidWallets.{Abstractions,EventStore.*,Reporting} ./src/
cp -r ../PolarSharp/src/PolarSharp.PrepaidWallets ./src/

# Strip the PolarSharp. prefix from directory names + namespaces
find ./src -type d -name "PolarSharp.PrepaidWallets*" -exec rename 's/PolarSharp\.PrepaidWallets/PrepaidWallets/' {} +
find ./src -type f \( -name "*.cs" -o -name "*.csproj" -o -name "*.md" \) -exec \
  sed -i 's/PolarSharp\.PrepaidWallets/PrepaidWallets/g; s/namespace PolarSharp\.PrepaidWallets/namespace PrepaidWallets/g' {} +

# Wire CI + publish to NuGet
```

That moves all 10 wallet-core packages to the new repo. Then **back in PolarSharp**,
the 4 `.Polar.*` bridge packages each bump their dependency:

```diff
- <ProjectReference Include="..\..\src\PolarSharp.PrepaidWallets.Abstractions\PolarSharp.PrepaidWallets.Abstractions.csproj" />
+ <PackageReference Include="PrepaidWallets.Abstractions" Version="1.0.0" />
```

…and rebuild + republish the 4 bridge packages.

**Public API surface unchanged for any host that consumed the bridges.** The host's
existing `using PolarSharp.PrepaidWallets.Polar.Checkout;` keeps working; the bridge
internally swapped what it depends on, but its own surface didn't change.

---

## Files that get touched in the lift

### Files that MOVE (10 packages × ~20 files each ≈ 200 files)

```
src/PolarSharp.PrepaidWallets.Abstractions/
src/PolarSharp.PrepaidWallets/
src/PolarSharp.PrepaidWallets.EventStore.Marten/
src/PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore/
src/PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.SqlServer/
src/PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.Sqlite/
src/PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.PostgreSQL/
src/PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.MariaDb/
src/PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.CosmosDb/
src/PolarSharp.PrepaidWallets.Reporting/

tests/PolarSharp.PrepaidWallets.*.Tests/   ← entire test suite for wallet core
```

### Files that STAY (4 bridge packages)

```
src/PolarSharp.PrepaidWallets.Polar.Identity/
src/PolarSharp.PrepaidWallets.Polar.Checkout/
src/PolarSharp.PrepaidWallets.Polar.Reporting/
src/PolarSharp.PrepaidWallets.Polar.GraphQL/

tests/PolarSharp.PrepaidWallets.Polar.*.Tests/
```

### Files in PolarSharp that need 1-line edits per bridge package

For each of the 4 bridge csproj files: replace ProjectReference → PackageReference
(see diff above). That's 4 csproj edits, 1 line each.

### Files that are NEW in PolarSharp post-lift

A small "PolarSharp wires the external PrepaidWallets library" composition extension
in each bridge — but the bridge code was already authored to consume the wallet-core
interfaces, so the only change is the dependency type. No new files.

---

## Verification after the lift

These checks pass before merging the lift PR:

1. **PolarSharp CI green** — bridges still compile against `PrepaidWallets.*` package refs.
2. **Wallet-core CI green in new repo** — same test suite, just renamed.
3. **A representative host project** — the integration test that exercises the full
   wallet checkout flow should run unchanged. Same `using` statements, same DI wiring.
4. **`dotnet list package --include-transitive`** on each bridge package shows
   `PrepaidWallets.*` deps (the new external lib) where it previously showed
   `PolarSharp.PrepaidWallets.*` (project refs). No PolarSharp internals leak into
   wallet core in either repo.
5. **NuGet feed** — the new `PrepaidWallets.*` packages publish to nuget.org (or your
   chosen feed) with version 1.0.0, matching the version you cut PolarSharp at on lift day.

---

## Why this works

The bridges hold ALL the PolarSharp coupling. Wallet core never knows PolarSharp
exists. So the lift is mechanical, not architectural.

The CI guard prevents drift. The interface seams provide the integration points.
The namespace separator makes the boundary visible to any developer.

The lift becomes a 30-minute PR, not a multi-week refactor.

---

## When NOT to lift

Don't lift if:

- The wallet feature gets minor adoption — keeping it inside PolarSharp keeps the
  release cadence simpler (one CI, one set of versioned packages).
- The wallet core needs to start depending on PolarSharp internals for some reason
  (unlikely given the design, but possible if scope expands significantly).
- The lift would split a release boundary mid-work (wait for v1.3.x to settle first).

Lift WHEN:

- A second host product (outside PolarSharp) wants to use the wallet — the strongest
  signal that wallet core deserves its own SemVer cycle.
- Wallet-specific feature requests start outpacing PolarSharp's release cadence.
- A separate team takes ownership of wallets.

---

## See also

- `/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md` —
  the v1.3.0 expansion plan, "Prepaid wallets — design detail (lift-and-shift
  architecture)" section for the full design rationale and package decomposition.
- The 14 PrepaidWallets package csproj files (scaffolded in v1.3.0 Phase 12,
  commit `0bb3075`) — already structured per this contract.
- `scripts/verify-prepaidwallets-no-polarsharp-deps.sh` (will land in Phase 22) —
  the CI guard that enforces the contract.
