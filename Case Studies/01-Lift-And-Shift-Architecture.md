# Case Study 01 — Lift-and-Shift Architecture for Composable .NET SDKs

> **Author**: Mark Chipman — Molls and Hersh, LLC.
> **Date**: 2026-05-15
> **Status**: Stable. Reference implementation: PolarSharp.PrepaidWallets v1.3 (28 lift-safe packages + 8 Polar bridges).
> **License**: © Mark Chipman / Molls and Hersh, LLC. 2026. Educational use permitted with attribution.
> **Related files**: [`PrepaidWalletsLiftAndShift.md`](../PrepaidWalletsLiftAndShift.md) (the operational lift procedure)

## TL;DR

A feature lives inside a monorepo for ease of co-development but is **architected from day one** so that — at any point in the future — it can be lifted into its own GitHub repository with a 5-line script and no architectural rework. The boundary between "feature core" (which moves) and "monorepo integration" (which stays) is enforced by a visible namespace separator AND a CI dependency-graph guard that fails the build if forbidden coupling is introduced.

## Historical context / inspiration / prior art

Most modular monorepo strategies focus on internal *code organization* (Nx workspaces, Lerna, Bazel) — they make it easier to work with many packages in one repo, but they don't make extraction easy. Microservice patterns (the bounded-context idea from Domain-Driven Design) push toward separate repos from day one, but pay the cross-cutting-changes cost early and ship slower in small teams.

This pattern emerged from an observation: most "we should split this out into its own repo" conversations happen 2–3 years into a project's life, by which point the coupling has metastasized and the extraction takes weeks of architectural surgery. By that time, the original reason for extraction (a second consumer wants the feature; the team wants independent release cadence; the feature outgrew the parent project) has often gone away because the cost was too high.

The lift-and-shift architecture inverts the trade-off: pay the boundary-discipline cost up front (small ongoing tax during co-development), buy the option to extract cheaply (5-line script, days not weeks). The CI guard is the discipline that makes the option real — without mechanical enforcement, the boundary erodes silently over years.

The closest prior art is the "anti-corruption layer" from Domain-Driven Design and the "ports and adapters" / hexagonal architecture pattern from Alistair Cockburn — both of which advocate for isolating a core domain behind explicit interface boundaries. The lift-and-shift pattern extends this with: (a) a *visible* boundary signaling mechanism (namespace separator), and (b) *mechanical* CI enforcement of the boundary as a first-class build gate.

## Mental model: the household-and-divorce analogy

A useful way to think about the prior and post states of a lift-and-shift is by analogy to a couple before and after a divorce. The analogy is imperfect — no families are nuget packages, no developers are spouses — but it captures something the more technical framings miss: the *qualitative* difference in what is easy and what is hard at each stage, and the importance of doing certain kinds of work *before* the separation rather than after.

### The unified household (pre-lift)

Before a couple separates, everyone lives under one roof. House rules are stipulated and enforced top-down: bedtimes, chore charts, dietary expectations, the rule that everyone eats dinner at the same table. The arrangement makes certain kinds of work cheap. Coordinating schedules is trivial. Spotting when someone is unwell is immediate. A missing toothbrush gets noticed because it lives in a shared bathroom. The interdependence is the very thing that makes the dynamics legible and the rules enforceable.

A single monorepo holding many interrelated packages under one unifying solution file behaves the same way. While the packages live together:

- **Cross-package boundaries are easy to discover** because every file is one IDE click away. A developer wondering "what calls this method?" answers the question in seconds across the entire estate.
- **Gaps in coverage become visible** as soon as someone wires the packages together for a real scenario. A missing interface, an unhandled edge case, an undocumented assumption — all of these surface during integration because the integration happens in the same repository the developer is already working in.
- **Regression, unit, and smoke tests can be authored holistically.** A test that exercises three packages together lives in one place, runs in one CI pipeline, and breaks visibly when any of the three packages misbehaves.
- **Specialized test and demo applications can illustrate the full wiring story.** A "kitchen sink" example app that registers every package, demonstrates the happy path, and exercises the error paths is straightforward to build and maintain when every package is one project reference away.
- **The full specification can be developed and re-developed iteratively.** When a missing feature surfaces late, adding it across multiple packages happens in a single pull request. The team does not have to coordinate releases across six repositories to ship one logical change.
- **Rules and expectations can be enforced through influence rather than contracts.** Code review, shared style guides, "we agreed last sprint not to do that" — these soft mechanisms work because everyone is in the same room. The closest analog in a multi-repo world is a written contract negotiated up front, which is much heavier.

This is the **prior state**, and a great deal of the architectural value of the lift-shift pattern comes from doing the hard work of design, implementation, and comprehensive testing *while still in this state*. The unified household is the right venue for stipulating boundaries, codifying expectations, building comprehensive demo applications, and confirming through executable tests that the whole estate works end-to-end. The CI dependency guard described later in this document exists precisely so that this prior-state work can proceed with full coordination ergonomics while still preserving the option to separate cleanly later.

### Families of packages move together

When a household does separate, related members usually stay together. The spouse moving out typically takes the children, the family pet, and the household items that belong with them — not a random scattering of objects. The new household is recognizably *that part of the original household*, reassembled with its internal relationships intact.

A lift-and-shift extraction works the same way. A feature family — a parent package and its supporting siblings, providers, adapters, and integrations — moves together to a single new repository. The packages that belonged together before the lift still belong together after the lift; the relationships between them are preserved verbatim. A wallet feature with a dozen storage providers and notification channels moves as one family. An ecommerce feature with its catalog providers, search adapters, theme presets, and pipeline stages moves as one family. Splitting a feature family across two new repositories during the lift would defeat the entire purpose of having designed the family as a cohesive unit in the first place.

### Setting up the new household

In the new home, things are different in ways that go beyond a change of address. The rules that one party had assumed were universal turn out to have been *house rules* — specific to the prior arrangement, not laws of nature. Coordinating with the other household now requires explicit messages instead of shouting across the kitchen. Schedules need to be agreed in advance instead of discovered ad hoc. The interdependence that made everything legible has been replaced by a clear boundary and intentional contact across it.

The first concrete act of the post-lift state mirrors moving into a new home: setting it up so the family that just arrived can actually live there. In the new repository, that means:

1. **Creating a fresh solution file.** A new `.slnx` (or `.sln`) in the new repository, owned by the new repository.
2. **Adding every lifted project to the new solution.** Each `.csproj` that came over in the family lift gets added to the new solution file.
3. **Wiring up the project references between the lifted packages.** Inside the new solution, every relationship that previously existed between the packages — `ProjectReference` to a sibling, shared interface implementations, transitive dependency chains — is recreated explicitly. The shape of the internal dependency graph is preserved exactly as it existed in the prior solution, but now bounded entirely within the new repository.
4. **Verifying internal cohesion.** The new solution builds, tests, and publishes without reaching into the prior repository for anything. If a build error reveals a forgotten dependency on the prior estate, that dependency was a coupling the lift-shift design was meant to prevent — and it needs to be removed, abstracted away, or duplicated as a lift-safe equivalent before the new household is truly self-sufficient.

### The new household after settlement

What was previously a `ProjectReference` from a sibling package in the parent monorepo becomes a normal NuGet `PackageReference` from a published feed, and the lifted family is consumed like any other third-party library. Including, critically, by the original parent project itself: the parent's integration bridges can be re-pointed at the published lifted library and continue to fulfill their role unchanged. The family that left the household can come back as a guest — invited, useful, but no longer a resident, and bound now by the same external contract as any other consumer.

The post-lift state is not the end of the relationship between the two estates. It is the beginning of a different kind of relationship: bounded by explicit contracts (published NuGet versions, semantic-versioning commitments, changelog entries) instead of in-monorepo intimacy. Done well, the parent project ends up *more* stable in this arrangement, because what was previously a tightly-coupled internal subsystem is now an externally-versioned dependency that can only change in ways the parent has explicitly opted into.

### Why this framing matters

The household analogy makes one thing visible that pure architectural framings tend to obscure: **the lift itself is not the goal**. The lift is an option, exercised when external circumstances make it the right call (a second consumer, an organizational change, a divergent release cadence, an acquisition, a deprecation). The *real* purpose of the pattern is to preserve the cheap-coordination, easy-testing, full-spec-development virtues of the unified household for as long as it serves the project, while leaving open the option of a cleanly-executed separation when the time comes.

Designing for liftability is the same kind of forethought as a couple keeping their important documents organized, their financial accounts well-titled, and their household inventory clear from the start of the relationship. Most of the time it is a minor ongoing cost — a slight tax on day-to-day life. On the day it matters, it is the difference between a clean transition and a years-long ordeal. The mechanical enforcement described in the rest of this document (the namespace separator and the CI dependency guard) is the equivalent of the practical disciplines that make any such transition feasible: not assumed, but verified.

## Problem

Consider a SDK that grows to encompass many concerns — payments, identity, catalog, reporting, prepaid wallets, embedded widgets, search, etc. Some of these concerns are tightly coupled to the SDK's primary purpose; others are genuinely independent (a wallet is a wallet; an embeddable widget is an embeddable widget). The independent concerns might one day:

- Be useful to projects that don't use the parent SDK at all
- Outgrow the parent SDK's release cadence and need their own
- Be acquired or transferred to a different team or organization
- Be open-sourced separately while the parent SDK stays proprietary (or vice versa)
- Need to survive the parent SDK's deprecation or pivot

The naive approach — "we'll extract it later if needed" — fails because by the time "later" arrives, the cost of extraction is so high that the question becomes "is this worth a 3-month engineering project?" and the answer is usually "no, leave it where it is." The feature stays trapped in a parent that may no longer be the right home for it.

## Forces / constraints

The design must satisfy several competing concerns:

1. **Co-development ergonomics matter.** A monorepo with cross-package change-in-one-PR is genuinely productive. Forcing every cross-package change into multiple PRs across multiple repos slows the team.
2. **Extraction must remain cheap.** Not just theoretically possible — *operationally* a few hours of work, not weeks.
3. **The boundary must be visible.** A developer adding code needs to know whether they're inside the lift-safe core or the integration bridge without consulting documentation.
4. **The boundary must be machine-enforced.** Human discipline erodes over years; only CI catches drift consistently.
5. **The boundary must not be a straitjacket.** Integration code that legitimately couples to the parent SDK needs a place to live AND must be clearly distinguishable from core code.
6. **The pattern must work in C# / .NET project structures.** No exotic build-system requirements; standard csproj, ProjectReference, PackageReference, and CI primitives.

## The pattern

A two-tier package decomposition with a **visible namespace separator** marking the lift boundary, plus a CI guard enforcing dependency cleanliness in the lift-safe tier.

```
Feature packages live in one of two tiers, distinguishable by namespace:

  Tier A: FEATURE CORE (lift-safe)
    Namespace pattern: PolarSharp.FeatureName.*       (no `.Polar.` infix anywhere)
    Allowed dependencies: third-party libs only
                          (Microsoft.Extensions.*, EF Core, etc.)
                          NO PolarSharp.* references whatsoever.
    On lift: MOVES verbatim to the new repo via namespace rename
             (PolarSharp.FeatureName → FeatureName).

  Tier B: POLAR INTEGRATION BRIDGES (stay)
    Namespace pattern: PolarSharp.FeatureName.Polar.*  (with `.Polar.` infix)
    Allowed dependencies: feature core (Tier A) +
                          any PolarSharp.* internal packages.
    On lift: STAYS in PolarSharp. ProjectReference to Tier A package becomes
             PackageReference to the lifted external library.
             Public API surface unchanged for consumers.
```

The `.Polar.*` infix is the entire visible-boundary contract. Any developer adding code with a namespace containing `.Polar.` knows they're in the bridge tier and can use PolarSharp internals freely. Any developer adding code without `.Polar.` in the namespace knows they're in the lift-safe core tier and PolarSharp internals are forbidden.

The CI guard runs `dotnet list package --include-transitive` on every Tier A package and fails the build if any `PolarSharp.*` package (other than another Tier A package in the same feature family) appears in the dependency graph. The guard is a 30-line bash script run on every PR. It is the entire mechanical enforcement.

## Implementation mechanics

### Step 1: Package decomposition

When introducing a new feature, classify each package as Tier A or Tier B before writing code:

```
NewFeature.Abstractions                   → Tier A (interfaces + DTOs only)
NewFeature                                → Tier A (core domain logic)
NewFeature.Storage.SqlServer              → Tier A (provider impl)
NewFeature.Storage.PostgreSQL             → Tier A (provider impl)
NewFeature.Storage.Sqlite                 → Tier A (provider impl)
NewFeature.AspNetCore                     → Tier A (host-platform integration; only Microsoft.* deps)
NewFeature.Polar.IntegrationConcernA      → Tier B (couples to PolarSharp.X)
NewFeature.Polar.IntegrationConcernB      → Tier B (couples to PolarSharp.Y)
```

### Step 2: Interface seams as the integration points

The Tier A core defines abstractions (`INewFeatureContextProvider`, `INewFeatureRecipientResolver`, etc.) for everything that might need PolarSharp-specific implementation. The core ships a default implementation that works without PolarSharp (single-tenant fallback, no-op, etc.). Tier B bridges provide PolarSharp-aware implementations that the host registers via DI to override the defaults.

The key discipline: **Tier A NEVER imports a PolarSharp.* type, even for type-information purposes.** If the abstraction needs to describe a value object that PolarSharp also has, Tier A defines its OWN type and the bridge does the mapping. Yes, this means duplicate types in some cases. Yes, that's an acceptable cost for the lift-shift option.

### Step 3: CI dependency guard

```bash
#!/bin/bash
# scripts/verify-feature-no-polarsharp-deps.sh

set -e

TIER_A_PACKAGES=(
  "src/PolarSharp.NewFeature.Abstractions"
  "src/PolarSharp.NewFeature"
  "src/PolarSharp.NewFeature.Storage.SqlServer"
  # ... all Tier A packages
)

for pkg in "${TIER_A_PACKAGES[@]}"; do
  echo "Checking $pkg for forbidden PolarSharp.* dependencies..."
  forbidden=$(dotnet list "$pkg" package --include-transitive 2>/dev/null \
    | grep -E "^\s+>\s+PolarSharp\." \
    | grep -v "PolarSharp.NewFeature" \
    || true)
  if [ -n "$forbidden" ]; then
    echo "FAIL: $pkg has forbidden PolarSharp.* dependencies:"
    echo "$forbidden"
    exit 1
  fi
done

echo "PASS: All Tier A packages are lift-safe."
```

This script runs in CI on every PR. If a developer adds `using PolarSharp.SomeThing;` to a Tier A package, the build fails. The boundary is mechanically enforced from day one and stays enforced forever.

### Step 4: The lift script (5 lines + CI rewiring)

When the time comes to extract:

```bash
# In a new feature repo:
git clone the-new-repo && cd the-new-repo
cp -r ../parent-repo/src/PolarSharp.NewFeature.{Abstractions,Storage.*,AspNetCore} ./src/
cp -r ../parent-repo/src/PolarSharp.NewFeature ./src/

# Strip PolarSharp. prefix:
find ./src -type d -name "PolarSharp.NewFeature*" -exec rename 's/PolarSharp\.NewFeature/NewFeature/' {} +
find ./src -type f \( -name "*.cs" -o -name "*.csproj" -o -name "*.md" \) -exec \
  sed -i 's/PolarSharp\.NewFeature/NewFeature/g; s/namespace PolarSharp\.NewFeature/namespace NewFeature/g' {} +

# Wire CI + publish to NuGet
```

Back in the parent repo, the Tier B bridge packages bump their `<ProjectReference>` to `<PackageReference>` against the new external NuGet feed:

```diff
- <ProjectReference Include="..\..\src\PolarSharp.NewFeature.Abstractions\PolarSharp.NewFeature.Abstractions.csproj" />
+ <PackageReference Include="NewFeature.Abstractions" Version="1.0.0" />
```

Public API surface of the bridge packages is unchanged. Hosts who consumed the bridges see no breaking changes. The extraction is complete in hours, not weeks.

## Worked example (from PolarSharp)

PolarSharp.PrepaidWallets (v1.3) is the reference implementation:

- **28 lift-safe packages** under `PolarSharp.PrepaidWallets.*` (no `.Polar.` infix). Categories include: Abstractions, event-sourced core, 5 EF Core storage providers, Marten storage provider, Reporting, AspNetCore.Identity (single-tenant integration), AspNetCore.GraphQL, Notifications + 6 channel providers (SendGrid / MailKit / Azure / AWS SES / Twilio / Webhook), 2 template engines (Scriban + Fluid), 2 funding processors (Stripe + PayPal), AspNetCore.SignalR, 2 Blazor RCLs (Customer + Admin), SaaSInvoicing, PrefundedTenant.
- **8 Polar bridges** under `PolarSharp.PrepaidWallets.Polar.*`: Identity, Checkout, Reporting, GraphQL, PurchaseOrder, Notifications, Translation, SaaSInvoicing.
- **CI guard**: `scripts/verify-prepaidwallets-no-polarsharp-deps.sh` runs on every PR.
- **Lift procedure**: documented in [`PrepaidWalletsLiftAndShift.md`](../PrepaidWalletsLiftAndShift.md) — a 5-line bash script.

The wallet feature can become its own standalone `PrepaidWallets/*` repo at any point. The 8 bridges stay in PolarSharp and rewire to consume the external library. Hosts using the bridges see no API change.

## Trade-offs

**What you give up:**

1. **Some code duplication.** The lift-safe core can't import PolarSharp types, so where the two ecosystems both have, say, a "tenant transaction context" concept, each defines its own. The bridge does the mapping. The duplication is small (typically a few records per feature) but real.
2. **Abstraction overhead.** Every PolarSharp-integration concern lives behind an interface in the core (e.g., `IWalletIdentityProvider` rather than directly calling `ICurrentUser`). This adds one layer of indirection. Modern .NET DI makes this cheap but it's not zero.
3. **Cognitive cost during development.** Developers think "wait, am I in Tier A or Tier B right now?" Once habituated this becomes second nature, but it's a real onboarding cost for new team members.
4. **More packages.** A feature that might have been 3 packages becomes 6–8 because the bridges are separate. This means more csproj files, more NuGet metadata to maintain, more pipeline steps.

**Alternative patterns and why this one over those:**

- **Microservices from day one** — pays the cross-cutting-changes cost continuously; only worth it if the team is already large enough that PR coordination across services is cheaper than monorepo branch coordination.
- **Single-package monolith** — easiest co-development but extraction is essentially impossible later.
- **Plain interface-based decoupling (no namespace separator, no CI guard)** — the discipline erodes over years; the option to extract evaporates without anyone noticing.

The lift-shift pattern is specifically targeted at projects where (a) co-development ergonomics matter NOW, (b) extraction MIGHT matter later, and (c) you cannot afford to lose the extraction option silently. If any of those three is missing, simpler patterns may serve better.

## Failure modes

**Mode 1: Silent coupling drift.** A developer adds `using PolarSharp.X;` to a Tier A file. **Detection**: CI guard fails immediately on the PR. **Recovery**: developer removes the import OR moves the file to a bridge package OR adds a new abstraction in the core that the bridge implements.

**Mode 2: Over-bridging.** Developers create bridges so trivially-thin that they add cognitive load without value. **Detection**: code review notices a bridge package whose only purpose is to expose a single interface method that could have lived in the abstraction directly. **Recovery**: consolidate the bridge logic; ensure each bridge package has at least one meaningful integration concern.

**Mode 3: Abstraction explosion.** The Tier A core grows so many "in case we ever need to swap this out" interfaces that the abstraction layer outweighs the implementation. **Detection**: ratio of interface files to concrete-class files; subjective code-review smell. **Recovery**: collapse unused abstractions; only keep interfaces for integration concerns that are *actually* served by multiple bridge implementations OR that are explicitly required for the lift contract.

**Mode 4: CI guard becomes a bottleneck via flaky transitive resolution.** `dotnet list package --include-transitive` can be slow or flaky in some configurations. **Detection**: CI build times grow. **Recovery**: cache the dependency graph between PRs; only re-run the guard on PRs that touch the relevant packages; in extreme cases, reimplement the guard against the lock file directly rather than via `dotnet list`.

**Mode 5: Allow-list creep.** When the guard fails for a legitimate hotfix reason, developers might allow-list the violation rather than fix it. Over time the allow-list grows and the contract degrades. **Detection**: any allow-list entry should require an associated tracking issue with a removal commitment; periodic audits. **Recovery**: aggressive cleanup of stale allow-list entries before each minor version bump.

## When to use this elsewhere

**Signs this pattern fits your project:**

- A subset of your codebase has clear standalone-product potential.
- You expect the codebase to live for 5+ years.
- A second consumer (different team, different product) might use the feature later.
- The feature is being incubated inside a larger project but might "graduate" to its own home.
- The parent project's release cadence might not match the feature's needs long-term.
- The feature is being built by a contractor / acquired team / external dependency that might pivot.

**Signs this pattern is overkill:**

- The feature is genuinely inseparable from its parent (e.g., the parent's authentication system).
- The parent project has a clearly-time-bounded lifetime (a 6-month proof-of-concept).
- The team is small enough that extraction discussions can be ad-hoc.
- The feature is so simple that one package suffices.

## Adaptation checklist

When applying this pattern to a new feature in a new project, follow these steps in order:

1. **Name the feature** in a way that's PolarSharp-independent (`NewFeature`, not `MyPlatformNewFeature`). The PolarSharp-prefix is only added for the in-monorepo packages.
2. **Decide the namespace separator string.** For PolarSharp it's `.Polar.`. For your project it might be `.{YourProjectName}.` or `.Bridge.`. Pick something visible and Google-able.
3. **List every conceivable package upfront.** Classify each as Tier A or Tier B. Resist the urge to start coding before this list is solid.
4. **Author the Tier A abstractions FIRST.** Before writing any concrete impl, define the interfaces that the bridges will fulfill. This forces the boundary to be designed explicitly.
5. **Write the CI guard script SECOND.** Before merging any feature code, the guard must run green on the empty scaffold. This proves the boundary holds before code arrives to test it.
6. **Author one bridge as a reference.** Pick the simplest integration concern (typically identity/auth) and build the bridge end-to-end. This validates the interface design.
7. **Document the lift procedure.** Write the equivalent of `PrepaidWalletsLiftAndShift.md` for your feature. The procedure must exist in writing before the feature ships v1.0.
8. **Test the lift periodically.** Once a year, branch the parent repo, run the lift script in an experimental branch, verify the extracted feature still builds cleanly. This is the only way to catch boundary drift the CI guard misses (e.g., implicit assumptions about parent build infrastructure).
9. **Resist the urge to extract prematurely.** The point of the pattern is to make extraction CHEAP later, not to extract NOW. Extract when there's a real reason (second consumer, divergent release cadence, organizational change), not when extraction becomes possible.
10. **Update the lift documentation with every new package.** When you add `NewFeature.NewBridge.X`, update the lift doc to enumerate it as a "stays" package. When you add `NewFeature.NewCore.X`, update the doc to enumerate it as a "moves" package and update the lift script's bash glob.

## Discussion / open questions

- **Is the namespace separator a fragile boundary?** A typo could classify a package wrong. The CI guard catches the *consequence* (forbidden dep) but not the *cause* (mis-classified package). One could imagine a second guard that verifies the namespace pattern matches the dependency profile.
- **How does this interact with `InternalsVisibleTo`?** Tier A packages may grant InternalsVisibleTo to other Tier A packages in the same feature family. Granting InternalsVisibleTo to a PolarSharp.* package outside the family would itself be a forbidden coupling — the CI guard should check for this too.
- **What about source-only packages?** A source-only package with content shipped under the consumer's namespace could blur the tier boundary. Recommend explicit prohibition on source-only packages in the lift-safe core.
- **Should the Tier B bridge packages also have a CI guard?** They can use PolarSharp internals freely, so a guard isn't a contract enforcement — but a guard that verifies "this bridge package depends on at LEAST one PolarSharp.* package" prevents accidental misclassification (a "bridge" that doesn't actually bridge anything is suspicious).
- **What about multi-feature lift?** Two features both lift-safe with their own CI guards — can they lift together to a single new repo? The current pattern handles them as independent lifts; cross-feature shared types would need to be in a third lift-safe shared package.

## Related patterns

- **Case Study 02 — Event-Sourced Wallet with Comprehensive Economic Modeling** — the wallet's recursive TenantPrefundedWallet design depends on the wallet being lift-safe (the wallet contains other wallets via meta-tenancy). The lift-shift pattern is a prerequisite.
- **Case Study 05 — Multi-Tenancy as Optional for Library Authors** — the lift-safe core must support both single-tenant and multi-tenant deployments because external consumers post-lift may have different needs. The two patterns reinforce each other.
- **Domain-Driven Design — Anti-Corruption Layer** — Eric Evans' pattern for protecting a domain model from coupling to an external system. Lift-shift extends ACL with mechanical CI enforcement + the namespace-separator visibility mechanism.
- **Hexagonal Architecture (Ports and Adapters)** — Alistair Cockburn's pattern for isolating core logic behind explicit interface boundaries. Lift-shift adds the *liftability* dimension to ports-and-adapters: the ports stay; the adapters can become a separate library.

## Citation format

> Chipman, Mark. *Lift-and-Shift Architecture for Composable .NET SDKs*. PolarSharp Architectural Case Study 01. Molls and Hersh, LLC, 2026. https://github.com/mollsandhersh/Polar.sh_Nuget/tree/main/Case%20Studies
