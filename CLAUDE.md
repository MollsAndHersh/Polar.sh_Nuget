# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Purpose

This repository will contain a .NET NuGet package integrating with [Polar.sh](https://polar.sh) — an open source monetization platform. The package is expected to target .NET and follow standard NuGet packaging conventions.

## Required Reading Order

Before doing any work, read these files (from the home directory `/Users/mollsandhersh/`):

1. `AGENTS.md` — workflow rules, agentic-master policy, RAG policy
2. `PLAN.md` — active technical plan
3. `TASKS.md` — current task list
4. `PROGRESS.md` — completed work log
5. `DECISIONS.md` — locked architecture decisions
6. `ZoranHorvat.md` — required for all .NET/C#/NuGet work in this repo

## Build and Test

> Update this section once the project structure is established.

Expected commands once `.csproj` / `.sln` files exist:

```sh
dotnet build
dotnet test
dotnet pack           # produces the NuGet .nupkg
dotnet nuget push     # publish to NuGet feed
```

## Git and GitHub Policy

Do **not** run raw mutating Git/GitHub commands. Use `agentic-master` wrappers:

```sh
agentic-master new-task <TASK-ID>
agentic-master commit --ai
agentic-master push
agentic-master finish-task
agentic-master verify
```

See `~/AGENTS.md` for the full command reference.

## Architecture Notes

> Populate this section once source files exist.

- This is a .NET library project — apply ZoranHorvat.md coding standards to all `.cs` and `.csproj` files.
- NuGet package metadata (authors, description, version, license) belongs in the `.csproj` file via `<PackageId>`, `<Version>`, `<Description>`, etc., not in a separate `nuspec`.
- Public API surface should be minimal and intentional — treat every public type as a committed contract.

## Documentation Standards (PROJECT-WIDE, STANDING REQUIREMENT)

Every feature shipped in this repo must land with **complete documentation across all three surfaces** — there is no "we'll write the docs later" posture. This is a standing project requirement, not a per-feature ask.

### The three surfaces, in priority order

1. **Inline XML doc comments** (`///`) — every public type and every public member. CS1591 is build-error in `Directory.Build.props`. Cover `<summary>`, `<remarks>` for the *why*, `<param>` for each parameter, `<returns>`, `<exception>` for directly-thrown types. For public methods/extension methods also include `<example>` with compilable code.
2. **Per-package `README.md`** — every NuGet package ships its own README via `<PackageReadmeFile>README.md</PackageReadmeFile>`. Install command + ~5-line quickstart + link to the docs site.
3. **GitHub.io DocFX site** — conceptual articles for every capability area. Every new package adds at least one article to `docs/articles/*.md` and the corresponding entry to `docs/articles/toc.yml`. The DocFX `docfx build` step gates CI — broken cross-references fail the build.

### Implementation Narratives section (NEW — DocFX site)

The DocFX site has a dedicated **"Implementation Narratives"** top-level section providing approachable, audience-friendly guides covering:
- Every reusable Razor component shipped in `PolarSharp.UI.Components` (each component gets its own narrative)
- Major end-to-end workflows that touch multiple capability areas (e.g., "Onboarding a new merchant from sign-up through first sale", "Setting up payouts when you've never used Stripe before", "Translating your catalog into a new market")

The Narratives section is **distinct from the API Reference and the Articles** by tone and audience: API Reference is for developers, Articles are for system-design readers, Narratives are for **non-technical stakeholders** — product owners, merchant operators, sales/support staff — who need to understand what a feature does and when to use it without becoming developers.

### Writing-style rules for Implementation Narratives

- **Friendly, conversational tone.** Write like you're explaining the system to a smart colleague over coffee, not lecturing a class.
- **Audience: non-technical.** Assume the reader is sharp but doesn't know what a SignalR hub, a Razor component, an EF Core query filter, or an OAuth flow is.
- **Lean on analogies.** Hard concepts get explained by comparison to familiar things:
  - *"Think of the multi-tenant filter like the front desk at a hotel — every request shows their room key (tenant id), and the desk only hands them mail addressed to their room."*
  - *"The toast channel works the way a sports score app pushes updates to your phone — the server pushes when something happens, you don't have to keep refreshing."*
  - *"A Razor component is like a Lego brick — built once with a specific shape, snapped into many different pages without changing the brick itself."*
- **Concrete scenarios beat abstract descriptions.** Every narrative opens with "Suppose a merchant wants to …" and walks through the user-visible flow first, with the mechanics second.
- **Call out options and notes explicitly.** Every narrative ends with a "Things to know" subsection listing optional parameters, edge cases, and gotchas in plain language.
- **No jargon without an explanation.** First use of any technical term gets a parenthetical or footnote. Subsequent uses can assume the term is known within the narrative.
- **Short paragraphs, generous whitespace.** This is documentation people will read on their phones during onboarding meetings, not engineering reference material.

### When this requirement applies

- Every new public type → inline XML docs at minimum
- Every new package → README + DocFX article
- Every new RCL component → Implementation Narrative
- Every major workflow change → Implementation Narrative covering the user-visible impact
- Every release → CHANGELOG entry summarising what readers need to know

This standard is encoded here so it carries forward across sessions, contributors, and feature waves without needing to be re-asked.
