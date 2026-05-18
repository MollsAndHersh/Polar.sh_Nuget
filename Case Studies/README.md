# PolarSharp Architectural Case Studies

> **Author**: Mark Chipman — Molls and Hersh, LLC.
> **Inception**: 2026-05-15
> **License**: © Mark Chipman / Molls and Hersh, LLC. 2026. Educational use permitted with attribution.

This folder contains design-pattern case studies extracted from the PolarSharp SDK. Each documents a piece of architectural reasoning that has independent value beyond the specific PolarSharp product — a pattern that can be applied to other systems, taught to other engineers, or referenced when an AI agent is tasked with building something analogous.

## Who these case studies are for

Two primary audiences:

**1. AI agents acting on behalf of a developer**
When an agent is asked to build a new system — a marketplace, a SaaS billing layer, an embeddable widget product, a multi-tenant service — these case studies provide the architectural framework the agent needs to make sound design decisions without re-deriving them from first principles. Each case study includes an explicit **Adaptation Checklist** the agent can follow step by step.

**2. Software engineers learning to design systems at this level**
The patterns documented here are not in widely-known engineering textbooks. They emerged from solving specific real-world problems in PolarSharp and proved to be more broadly applicable than their original context. Engineers reading these case studies can learn the patterns, understand the trade-offs, and apply them in their own work. Citation is appreciated.

## How these differ from API reference docs, articles, and Narratives

PolarSharp follows a 4-surface documentation strategy:

| Surface | Purpose | Audience |
|---|---|---|
| **Inline XML doc comments** | Reference: what each public type / member does | Developers using the API |
| **Per-package README** | Quick-start: install + 5-line example + link to deeper docs | Developers evaluating the package |
| **DocFX articles** | Conceptual: how a capability area works end-to-end | Developers integrating the feature |
| **Implementation Narratives** | Audience-friendly explanations with analogies | Non-technical stakeholders (product owners, merchant operators) |
| **Case Studies (this folder)** | **Adaptation guides for the underlying architectural patterns** | **AI agents + software engineers applying the patterns in new contexts** |

A case study documents the WHY and the HOW behind a pattern in enough depth that someone can RECREATE the pattern in a new context — not just consume the existing implementation.

## The 5 case studies

| # | Title | Pattern documented | Status |
|---|---|---|---|
| 1 | [Lift-and-Shift Architecture for Composable Software Estates](./01-Lift-And-Shift-Architecture.md) | Two-tier package boundary via `.Polar.*` namespace separator + CI-enforced dependency guard | Stable (v1.3 PrepaidWallets reference implementation) |
| 2 | [Event-Sourced Wallet with Comprehensive Economic Modeling](./02-Event-Sourced-Wallet-With-Economic-Modeling.md) | Aggregate + MediatR + CQRS + projections + snapshots; configurable fee handling; dormancy hedging; B2B PO admin; recursive meta-tenant billing | Stable (v1.3 PrepaidWallets reference implementation) |
| 3 | [Embed-Anywhere Web Components with Server-as-Source-of-Truth](./03-Embed-Anywhere-Web-Components.md) | Stencil-compiled true Web Components + dynamic per-embed scoping + 3-layer real-time communication + 10-layer fraud prevention + framework-preset theming | Planned (v1.4 EcommerceStorefronts reference implementation) |
| 4 | [Audience-Scoped Schema Slicing for LLM-Driven Query Generation](./04-Audience-Scoped-Schema-Slicing.md) | Permission-derived schema slice per audience + structured-output enforcement + dry-run authz gating + cost/complexity gates + audit trail | Planned (v1.3 NaturalLanguageQuery reference implementation) |
| 5 | [Multi-Tenancy as Optional for Library Authors](./05-Multi-Tenancy-As-Optional.md) | Mode-agnostic abstractions + `IsMultiTenantMode` flag + audience-tier auto-collapse + query-filter skip in single-tenant mode | Stable (v1.3 PrepaidWallets reference implementation) |

## Common case-study structure

Every case study follows the same section structure so agents and humans can find the same information in the same place across all five documents:

1. **Metadata header** — author, date, status, license, related files
2. **TL;DR** — the pattern in 2–3 sentences
3. **Historical context / inspiration / prior art** — where the idea came from
4. **Problem** — concrete scenario the pattern solves
5. **Forces / constraints** — competing concerns that make the design hard
6. **The pattern** — the architectural decision with diagrams and code shapes
7. **Implementation mechanics** — step-by-step mechanism
8. **Worked example (from PolarSharp)** — concrete instance with file references
9. **Trade-offs** — what you give up; alternatives and why this over those
10. **Failure modes** — what goes wrong; how to detect and recover
11. **When to use this elsewhere** — decision criteria; signs of a fit
12. **Adaptation checklist** — step-by-step guide for applying in a new context
13. **Discussion / open questions** — invitations to debate; known unknowns
14. **Related patterns** — cross-references to other case studies
15. **Citation format** — how to cite this case study

## Attribution

All five case studies and the patterns they document are authored by **Mark Chipman — Molls and Hersh, LLC.** and were originally invented in the course of designing the PolarSharp .NET SDK ecosystem. If you adapt these patterns in your own work, attribution is appreciated.

Suggested citation:

> Chipman, Mark. *PolarSharp Architectural Case Studies: [Title]*. Molls and Hersh, LLC, 2026. https://github.com/mollsandhersh/Polar.sh_Nuget/tree/main/Case%20Studies
