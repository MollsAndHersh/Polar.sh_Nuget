---
title: "Event-Sourced Wallet with Comprehensive Economic Modeling"
short_title: "Event-Sourced Wallet"
case_study_id: "02"
author:
  name: "Mark Chipman"
  organization: "Molls and Hersh, LLC"
date_published: 2026-05-15
date_modified: 2026-05-18
status: stable
license: "© Mark Chipman / Molls and Hersh, LLC. 2026. Educational use permitted with attribution."
reference_implementation: "PolarSharp.PrepaidWallets v1.3 (36 packages; 10 wallet-core tables; 4 exclusive settlement modes; 3 fee-handling modes; recursive TenantPrefundedWallet meta-tenant pattern)"
keywords:
  - event sourcing
  - CQRS
  - MediatR
  - aggregate pattern
  - projections
  - snapshots
  - wallet ledger
  - token economics
  - fee handling
  - dormancy hedging
  - refund-as-credit
  - B2B purchase orders
  - meta-tenant billing
  - recursive tenancy
  - prepaid wallet
related_case_studies:
  - "01-Lift-And-Shift-Architecture"
  - "05-Multi-Tenancy-As-Optional"
related_patterns:
  - "Domain-Driven Design — Aggregate"
  - "CQRS / Command Query Responsibility Segregation"
  - "Event Sourcing (Greg Young / Vaughn Vernon)"
  - "Optimistic Concurrency Control"
ecosystems:
  primary: ".NET / NuGet"
  generalizes_to: ["Marten (Postgres)", "EF Core providers", "multi-currency systems", "B2B billing systems"]
---
<!--
JSON-LD structured data (invisible on GitHub render; consumed by web crawlers + ontology-aware AI agents):
{
  "@context": "https://schema.org",
  "@type": "TechArticle",
  "headline": "Event-Sourced Wallet with Comprehensive Economic Modeling",
  "alternativeHeadline": "Event-Sourced Wallet",
  "author": {
    "@type": "Person",
    "name": "Mark Chipman",
    "affiliation": {
      "@type": "Organization",
      "name": "Molls and Hersh, LLC"
    }
  },
  "datePublished": "2026-05-15",
  "dateModified": "2026-05-18",
  "inLanguage": "en",
  "keywords": "event sourcing, CQRS, MediatR, aggregate pattern, projections, snapshots, wallet ledger, token economics, fee handling, dormancy hedging, refund-as-credit, B2B purchase orders, meta-tenant billing, recursive tenancy, prepaid wallet",
  "about": [
    "event sourcing",
    "aggregate pattern",
    "MediatR + CQRS pipeline",
    "wallet ledger",
    "configurable fee handling",
    "dormancy hedging",
    "refund-as-credit",
    "B2B purchase-order management",
    "recursive meta-tenant billing",
    "progressive balance escalation"
  ],
  "isPartOf": {
    "@type": "CreativeWorkSeries",
    "name": "PolarSharp Architectural Case Studies"
  },
  "license": "© Mark Chipman / Molls and Hersh, LLC. 2026. Educational use permitted with attribution.",
  "proficiencyLevel": "Expert"
}
-->

# Case Study 02 — Event-Sourced Wallet with Comprehensive Economic Modeling

> **Author**: Mark Chipman — Molls and Hersh, LLC.
> **Date**: 2026-05-15
> **Status**: Stable. Reference implementation: PolarSharp.PrepaidWallets v1.3 (36 packages; 10 wallet-core tables; 4 exclusive settlement modes; 3 fee-handling modes; recursive TenantPrefundedWallet meta-tenant pattern).
> **License**: © Mark Chipman / Molls and Hersh, LLC. 2026. Educational use permitted with attribution.
> **Related files**: [`PrepaidWalletsLiftAndShift.md`](../PrepaidWalletsLiftAndShift.md); the wallet design subsection in the v1.3.0 plan.

## TL;DR

A prepaid-token wallet is built on an event-sourced aggregate (immutable event log + projections + snapshot strategy) with FULL economic modeling baked into the design from day one: configurable fee-handling modes (who pays processing costs), 4 mutually-exclusive SaaS-revenue settlement modes (including a recursive meta-tenant "the tenants are themselves wallet customers" model), dormancy hedging (refund eligibility windows, surcharges, monthly maintenance fees on stagnant balances), B2B purchase-order admin (tenants accept POs from their own customers and credit wallets against them), and refund-as-credit (refunds route back to wallet tokens rather than the original payment instrument to save processor fees). Every fund/debit/credit/refund/maintenance-fee event records the FULL economic breakdown immutably — customer charged, processor fee, SaaS profit, tenant absorbed (if any), tenant net, tokens credited, funding-terms snapshot — so every audit question is answerable from the event log alone.

## Historical context / inspiration / prior art

The event-sourcing portion draws from established work: Greg Young's CQRS + Event Sourcing patterns (early 2010s), Vaughn Vernon's *Implementing Domain-Driven Design* (2013), and the practical Marten + Postgres event-store work that Jeremy Miller and the JasperFx team have shipped over the past decade. **Hannes Lowette's Dometrain course on Event Sourcing in .NET** is the canonical modern reference for the exact aggregate / command-handler / projection / snapshot pattern this case study adopts.

The economic-modeling portion is original. Most prepaid wallet implementations treat fees as a cost-of-doing-business absorbed silently, leaving the customer and the merchant to discover the actual numbers via their card statement (customer) or their monthly merchant statement (merchant). The "everything is in the event log including the fee breakdown" approach inverts that — economic transparency is a design goal of equal weight to functional correctness, and it composes naturally with event sourcing because events are already immutable and auditable.

The recursive **TenantPrefundedWallet** ("Option D") meta-tenant design — where each tenant is itself a wallet-holding customer in the SaaS's meta-tenant context, and platform fees debit the tenant's wallet in real time — is original and is the most novel piece of the design. The closest analogs are Twilio's prepaid balance + AWS Credits, but neither inverts the SaaS billing relationship into a recursive use of the same wallet system the SaaS sells to its own customers. The recursion is what makes the design elegant: the wallet machinery is used twice, once at the customer→tenant level and once at the tenant→SaaS level.

The dormancy-hedging pattern (180-day refund cutoff + 10% surcharge + monthly maintenance fee on stagnant balances) is borrowed from the prepaid-card / gift-card industry where it's universal. The contribution here is integrating it into an event-sourced ledger with per-funding-event terms snapshotting, so a customer's specific refund eligibility is locked at funding time and protected from later tenant policy changes.

The **refund-as-credit** pattern (returning a refund to the wallet rather than to the original payment instrument) is similarly common in gift-card-heavy retail but rare in general ecommerce. The contribution here is making it a first-class configurable mode that automatically saves the original-instrument refund fee whenever the customer is happy to be credited to wallet instead.

## Problem

A SaaS platform wants to give its tenants the ability to operate prepaid wallets for their own customers. The wallet must:

1. **Be auditable** — every dollar in, every token out, who did it, when, why, with all economic breakdown details intact even years later.
2. **Be fraud-resistant** — no client can fake balances or prices; double-debit attempts fail; concurrent operations are safe.
3. **Save processing fees** — the entire economic point of prepaid wallets is to amortize per-transaction fees by making one larger funding payment cover many small purchases. The wallet system must materially reduce the tenant's per-transaction processing costs.
4. **Be economically transparent** — customer, tenant, and SaaS all see the SAME breakdown of who paid what. No hidden fees, no "your card was charged $271 instead of $250" surprises.
5. **Be configurable per-tenant** — different tenants have different economic models; the wallet must support absorbing fees vs passing them through, different SaaS-revenue settlement mechanisms, different dormancy policies, different funding-processor preferences.
6. **Support B2B workflows** — tenants whose customers are themselves businesses receiving purchase orders need to be able to credit a wallet against a PO, track the PO's credit consumption, and reconcile against PO line items.
7. **Save the SaaS from being a credit-card middleman** — the SaaS itself must not need to take custody of customer payment instruments to take its cut of the economics.
8. **Survive lift-shift** — the wallet feature might one day be extracted to its own GitHub repo and used by projects unrelated to the original SaaS. The economic-modeling design must not be coupled to the original SaaS's identity / catalog / reporting systems.

## Forces / constraints

1. **Audit immutability vs schema evolution.** Event-sourced systems must replay events into the current aggregate shape across years of schema changes. Up-casting old event shapes is tedious but the alternative (locking the event schema forever) is worse.
2. **Projection eventual consistency vs query freshness.** Projections are eventually-consistent reads. UI components that need "current balance" can either query the aggregate (slow on long event histories) or query the projection (fast but might be stale by milliseconds). Snapshot strategy mediates this trade-off.
3. **Configurability vs simplicity.** Three fee modes × four settlement modes × dormancy on/off × three currently-shipping funding processors (extensible to others via `IWalletFundingProcessor` for hosts wanting Square, Adyen, Braintree, etc.) = many combinations. Each combination has to work correctly with no surprising emergent behavior.
4. **Fraud resistance vs UX friction.** Strong verification (idempotency keys on every operation, cart hash on every cart mutation, server-recomputed prices) protects against tampering but adds cognitive load for legitimate users if surfaced poorly.
5. **SaaS revenue must not require taking custody of customer payment.** The SaaS cannot be the payment processor; it must take its cut via mechanisms that flow money from tenant → SaaS WITHOUT routing customer credit cards through the SaaS's infrastructure.
6. **B2B vs B2C workflows in the same wallet.** Some tenants serve consumers (B2C); some serve businesses (B2B with POs); some serve both. The wallet must handle both without forcing a per-tenant fork.
7. **Lift-shift constraint.** The wallet's economic model must not couple to PolarSharp's catalog, identity, or reporting; bridges adapt those concerns. (See Case Study 01.)

## The pattern

The pattern decomposes into 8 distinct sub-patterns that compose into the full wallet design. Each can be evaluated and adopted independently.

### 1. Event-sourced aggregate as the source of truth

```
Wallet aggregate state = fold(all events for this wallet)

Commands return new events but do NOT persist them directly.
MediatR pipeline persists via IWalletEventStore.
Projections subscribe to events and update read models.
Snapshot strategy: persist aggregate state every N events; replay from snapshot + delta.
```

The aggregate enforces invariants (balance >= 0, no double-debit on same idempotency key, no funding while frozen, no debit on closed wallet). The event log is the audit trail.

### 2. The wallet event with full economic breakdown

```csharp
public sealed record WalletFunded(
    WalletId WalletId,
    long SequenceNo,
    DateTimeOffset OccurredAt,
    Guid ActorUserId,
    Option<string> SourceIpHash,
    Option<string> IdempotencyKey,
    TokenAmount Amount,
    FundingSource Source,

    // Economic transparency fields — REQUIRED, not optional:
    int CustomerChargedAmountCents,       // what was on the card
    int ProcessorFeeCents,                 // what Polar/Stripe/PayPal took
    int SaaSProfitCents,                   // what the SaaS platform took
    int TenantAbsorbedAmountCents,         // > 0 only if tenant is in AbsorbAndMarkup mode
    int TenantNetAmountCents,              // what the tenant nets to back the tokens
    long TokensCredited,                   // tokens added to wallet
    long BonusTokensCredited,              // promotional bonus, if any
    string FundingTermsSnapshotJson        // refund eligibility + surcharge + maintenance terms locked at funding time
);
```

This is not an audit *view* — it's the *event itself*. The fields are part of the immutable record. Years later, anyone can query "show me the breakdown of customer X's $271.36 payment on April 22" and get the exact numbers because they're in the event.

### 3. Three configurable fee-handling modes

`TenantBusinessProfile.WalletFundingFeeHandlingMode` selects one of three modes:

| Mode | Customer pays | Customer receives | Card statement | Tenant behavior |
|---|---|---|---|---|
| `AbsorbAndMarkup` | $250 face value | Exactly 25,000 tokens | $250 (matches button) | Tenant absorbs ~$19.67 in fees from margin. Marks up product pricing to recoup. Optional dual-price display on products. |
| `TransparentGrossUp` | $263.13 (computed) | Exactly 25,000 tokens | $263.13 (matches button) | Tenant nets $250 to back tokens. Quote step shows breakdown before customer commits. |
| `DollarFirstShowTokens` | $250 (entered) | 23,033 tokens (computed) | $250 (matches button) | Same economics as TransparentGrossUp; different framing. |

In all three modes, the WalletFunded event records the full breakdown. The mode is purely about UX framing + who eats which costs.

### 4. Four mutually-exclusive SaaS revenue settlement modes

`PolarSharp:SaaS:SettlementMode` is set platform-wide. The four modes:

| Mode | Funding processor compat | Mechanics |
|---|---|---|
| **A. StripeConnect** | Stripe only | At point of charge, Stripe Connect's `application_fee_amount` routes the SaaS's cut directly to the SaaS's connected account. Zero reconciliation; instant. |
| **B. BundledMonthlyInvoice** | All processors | Wallet accrues all SaaS cuts (funding cut + non-prepaid transaction cut + maintenance fee cut + refund surcharge cut) per tenant per month into `wallet_saas_profit_ledger`. SaaS's existing tenant-billing system bundles the accrued cuts as a line item on the tenant's regular monthly subscription invoice. |
| **C. StandalonePolarOrder** | All processors | Monthly cron generates a separate Polar Order from SaaS-org to tenant-org for the accumulated total. Tenant pays through their normal Polar billing relationship. |
| **D. TenantPrefundedWallet** | All processors | THE RECURSIVE MODE. Each tenant maintains its OWN tenant-wallet (the SaaS is a meta-tenant; tenants are customers in that meta-context). SaaS fees debit the tenant-wallet in real time. Progressive balance-escalation framework warns + escalates + suspends platform admin access when grace expires. Tenant-customer activity continues during tenant suspension (don't punish the tenant's customers for the tenant's billing problem). |

Modes are mutually exclusive — SaaS picks ONE platform-wide; `IValidateOptions` rejects startup if any tenant is configured with a funding processor incompatible with the chosen settlement mode.

### 5. Dormancy hedging via per-funding-event term snapshots

`TenantBusinessProfile.WalletFundingTerms` specifies:
- `RefundEligibilityDays` (null = always; 0 = never; default 180)
- `RefundSurchargePercent` (default 10.0)
- `MonthlyMaintenanceFeeTokens` (0 = disabled; e.g. 200 = $2/mo)
- `DormancyPeriodDays` (default 90)
- `DormancyWarningDays` (default 30)

These are SNAPSHOTTED on every `WalletFunded` event in `FundingTermsSnapshotJson`. If the tenant later changes their terms, existing tokens keep the original eligibility window, surcharge, and fee schedule. Contract immutability.

`WalletMaintenanceFeeService` is a daily IHostedService that walks active wallets, computes days-since-last-activity per wallet, fires `WalletMaintenanceFeeApplied` events for wallets past the dormancy threshold. Customer gets a notification N days before fees start (`WalletApproachingDormancy`).

Refund workflow: `RequestWalletRefundCommand` from customer → system computes eligible refund (within window, minus surcharge per the snapshotted terms) → tenant operator approves/denies → `WalletRefundCompleted` event → refund executed via original funding processor → tokens debited.

### 6. B2B purchase-order admin

Tenants whose customers are themselves businesses receive Purchase Orders. The wallet exposes a full `wallet_purchase_orders` + `wallet_purchase_order_lines` entity with status lifecycle (open → partial_credit → fully_credited → voided), unique `(customer_id, po_number)` index.

`WalletCredited` events linked via `RelatedPurchaseOrderId` track cumulative credits against the PO; status auto-updates when cumulative credits match the PO's total. Voiding a PO with prior credits fires a compensating debit.

### 7. Refund-as-credit

When a refund is issued on an order that debited a wallet, by default the refund credits tokens BACK to the wallet rather than refunding the original payment instrument. The tenant saves the processor's refund fee. The customer gets instant credit and is more likely to spend it again with this tenant.

Per-tenant `RefundConversionPolicy` configures behavior for hybrid orders (paid partially by wallet + partially by Polar): Proportional / WalletFirst / PolarFirst.

### 8. Real-time progressive balance-escalation framework

Generic `IBalanceEscalationPolicy` abstraction (reusable for both tenant-wallet and customer-wallet low-balance scenarios). Default tenant policy has 5 stages (Normal → Warning → Urgent → Critical → Suspended); default customer policy has 3 stages. Each stage configures threshold, notification frequency, channel set, message template. Auto-resets to Normal + fires `BalanceRestored` on top-up.

`WalletBalanceEscalationService` IHostedService runs hourly, applies the policy per wallet, fires notifications through the existing notification dispatcher (no parallel infrastructure — full reuse of the wallet's notification machinery).

## Funding flow: Polar.sh as the primary on-ramp

The wallet defines `IWalletFundingProcessor` in `PolarSharp.PrepaidWallets.Abstractions` as the contract for "something that can accept a payment from a customer and report back when the funds have cleared." Three concrete implementations ship with the wallet:

| Processor | Package | Tier | Use case |
|---|---|---|---|
| **Polar.sh** | `PolarSharp.PrepaidWallets.Polar.Checkout` | Polar bridge (stays on lift) | **Primary on-ramp for the SaaS tenant prepayment flow.** A tenant prepays for platform usage; the host creates a wallet-funding Polar Order via this processor; the customer (the tenant, in this context) completes payment on Polar.sh's hosted checkout; Polar fires the `order.paid` webhook; the bridge translates it into a `WalletFunded` event that credits the tenant's prepaid wallet at the configured token exchange rate. This processor is also the on-ramp for the entire Option D `TenantPrefundedWallet` settlement mode described in sub-pattern 4 above. |
| **Stripe** | `PolarSharp.PrepaidWallets.Funding.Stripe` | Lift-safe (moves with the wallet on lift) | Stripe Charges + optional Stripe Connect `application_fee_amount`. Required when the host picks settlement mode A (`StripeConnect`) platform-wide. |
| **PayPal** | `PolarSharp.PrepaidWallets.Funding.PayPal` | Lift-safe (moves with the wallet on lift) | PayPal Orders v2 + optional PayPal Payouts API. |

Hosts wanting other processors (Square, Adyen, Braintree, etc.) implement `IWalletFundingProcessor` themselves against the wallet abstraction — no fork of the wallet is required.

The Polar.sh path is uniquely important for the SaaS use case because it is the only one of the three shipped processors that is PolarSharp-coupled, and it is the on-ramp for the entire Option D recursive meta-tenant model. When the SaaS chooses Option D, every tenant prepays for platform usage via Polar.sh; those payments credit the tenant's own prepaid wallet; SaaS fees then debit that wallet in real time as the tenant operates and as the tenant's customers transact. The recursion is what makes Option D elegant — the wallet machinery is used twice, once at the customer-to-tenant level and once at the tenant-to-SaaS level — and the `PolarFundingProcessor` in `Polar.Checkout` is the on-ramp for both levels.

The end-to-end flow, in pseudocode:

```
1. Tenant operator clicks "Top up platform balance" in the SaaS's admin UI.
2. Host calls IWalletFundingProcessor.InitiatePaymentAsync(tenant.WalletId, amount).
3. PolarFundingProcessor (in Polar.Checkout) creates a Polar Order with the
   tenant's organization as the buyer + a wallet-funding metadata tag.
4. Customer (= the tenant in this context) is redirected to Polar's hosted
   checkout; pays with their chosen payment instrument.
5. Polar fires the order.paid webhook → PolarSharp.Webhooks → Polar.Checkout
   bridge handler.
6. Handler translates Polar order details into a FundWalletCommand carrying
   the full fee breakdown (customer charged amount, processor fee, SaaS profit,
   tenant absorbed, tenant net, tokens credited).
7. Wallet aggregate appends a WalletFunded event with all economic fields
   snapshotted into FundingTermsSnapshotJson.
8. WalletBalanceProjection updates; tenant sees the new balance in real time
   via SignalR push.
```

See `src/PolarSharp.PrepaidWallets.Polar.Checkout/README.md` for the package's full feature surface, and `PrepaidWalletsLiftAndShift.md` for how this bridge's funding-processor role survives a future lift of the wallet core.

## Implementation mechanics

### Step 1: Choose the event-store backend (Marten preferred where Postgres is available)

Marten 8.x runs on Postgres and provides native event-sourcing primitives (event streams, projection daemon, snapshot support). When the host's database is Postgres, Marten is the right choice — it eliminates a layer of glue code. For other databases (SQL Server, SQLite, MariaDB, Cosmos), use the EF Core event store with the schema:

```sql
wallet_events
  id, wallet_id, sequence_no, event_type, event_payload_json,
  idempotency_key, occurred_at, actor_user_id, source_ip_hash

wallet_snapshots
  wallet_id, sequence_no, balance, frozen, closed,
  snapshot_payload_json, taken_at
```

Optimistic concurrency via unique `(wallet_id, sequence_no)` index — second writer on a stale read fails with `WalletConcurrencyConflictException`; MediatR retry behavior handles.

### Step 2: Define the events with FULL economic breakdown fields from the start

Don't add the fee/profit/tenant-net fields later — they cost nothing to record at funding time but are impossible to reconstruct later. Make them required init properties on every relevant event.

### Step 3: MediatR pipeline with behaviors

```
Command → IdempotencyBehavior (lookup by idempotency-key; short-circuit if already processed)
        → ValidationBehavior (FluentValidation on DTO)
        → LoggingBehavior (structured log; redact PII)
        → TransactionBehavior (open event-store transaction)
        → CommandHandler (load aggregate, invoke command, return new events)
        → EventPersistor (append events, optionally snapshot, commit)
        → ProjectionDispatcher (publish events to subscribed projections)
```

### Step 4: Projections for read models

Default projections shipped:
- `WalletBalanceProjection` — O(1) current balance per wallet
- `WalletHistoryProjection` — paginated ledger per wallet
- `FundingSourceProjection` — funding-source attribution per wallet (which cards funded; which dates)

Plus the v1.3.0 amendment projections for the linked order/PO entities:
- `WalletProductSnapshotProjection`
- `WalletOrderProjection`
- `WalletPurchaseOrderProjection`
- `WalletSaaSProfitLedgerProjection` (per-tenant per-month accrual)

### Step 5: Snapshot strategy

Default: snapshot every 50 events. Cosmos DB: every 10 events (full replay is RU-expensive). Snapshots store the aggregate's denormalized state; load-from-snapshot then replay only events since the snapshot's sequence number.

### Step 6: IBalanceEscalationPolicy + IHostedService

Implement the policy as an abstraction so both tenant-wallet (5-stage default) and customer-wallet (3-stage default) cases use the same machinery. The hosted service runs hourly, walks wallets, applies the policy, dispatches notifications through the existing notification pipeline.

### Step 7: Per-tenant SaaSFeeRates settable only by AppMasterAdmin

Tenants must NOT be able to view or modify their own SaaS fee rates (those are the SaaS's pricing decisions). Gate the API with a `PolarPermission.ManageTenantBilling` permission that only AppMasterAdmin users hold.

## Worked example (from PolarSharp)

The PolarSharp.PrepaidWallets v1.3.0 implementation is the reference:

- **36 wallet packages** (28 lift-safe + 8 Polar bridges).
- **10 wallet-core tables**: wallet_events, wallet_snapshots, wallet_products, wallet_orders, wallet_order_lines, wallet_purchase_orders, wallet_purchase_order_lines, wallet_notifications, wallet_notification_preferences, wallet_notification_recipients.
- **5 EF Core storage providers** (SqlServer, Sqlite, PostgreSQL, MariaDb, CosmosDb) + Marten provider.
- **6 notification channel providers** (UI/in-app, SendGrid, MailKit, Azure Communication Email, AWS SES, Twilio, Webhook).
- **2 template engines** (Scriban + Fluid; tenants choose per-template).
- **3 funding processors**: Polar.sh (via the `Polar.Checkout` bridge — the primary on-ramp for the SaaS tenant prepayment flow; see the "Funding flow" section above), Stripe (`Funding.Stripe`, lift-safe), and PayPal (`Funding.PayPal`, lift-safe). Hosts wanting Square / Adyen / Braintree / etc. implement `IWalletFundingProcessor` themselves against the wallet abstraction — no wallet fork required.
- **4 exclusive settlement modes** (StripeConnect, BundledMonthlyInvoice, StandalonePolarOrder, TenantPrefundedWallet).
- **3 fee-handling modes** (AbsorbAndMarkup, TransparentGrossUp, DollarFirstShowTokens).
- **Progressive 5-stage escalation framework** (tenant wallet) + 3-stage (customer wallet); generic `IBalanceEscalationPolicy`.
- **Per-tenant SaaSFeeRates** (FundingCutPercent, NonPrepaidTransactionCutPercent, MaintenanceFeeCutPercent, RefundSurchargeCutPercent) on `TenantBusinessProfile`, AppMasterAdmin-only via `PolarPermission.ManageTenantBilling`.

The wallet ships as part of the v1.3.0 PolarSharp release and is structured per Case Study 01's lift-shift contract so it can be extracted to a standalone `PrepaidWallets/*` repo at any future date.

## Trade-offs

**What you give up:**

1. **Event-store schema management is harder than CRUD.** Up-casting old event shapes across schema evolutions takes ongoing engineering effort. Marten handles much of this; EF Core puts more burden on the developer.
2. **Projection rebuild storms.** When a projection schema changes, you replay the entire event log into the new projection. For tenants with millions of events, this can be slow. Marten's projection daemon handles this online; EF Core requires a maintenance window.
3. **Storage cost grows with event count.** Snapshots help (you can prune events older than the most recent snapshot) but storage is unbounded by default.
4. **Concurrency conflicts require retry logic.** Optimistic concurrency means second-writer failures on contended wallets; MediatR retry mitigates but adds latency.
5. **Configurability is testing surface.** 3 fee modes × 4 settlement modes × dormancy on/off × 3+ funding processors (open extension point via `IWalletFundingProcessor`) = many test combinations. Need a matrix-based test strategy.

**Alternative patterns and why this one over those:**

- **CRUD with audit log.** Simpler to implement; loses the replay-from-events benefits (no fix-the-past, no rebuild-projection-from-truth, weaker fraud audit).
- **Single fee mode hardcoded.** Simpler config; doesn't serve real tenant diversity (some want absorb-and-markup; some want transparent gross-up).
- **External wallet service (Stripe Issuing, Marqeta, etc.).** No code to maintain; but: subject to vendor pricing, can't customize the economic model, no lift-shift option, less data sovereignty.

## Failure modes

**Mode 1: Snapshot drift.** Bug in projection logic causes the snapshot to diverge from the event-replay truth. **Detection**: periodic batch comparison of snapshot vs replay; in-band assertion on hot-path loads. **Recovery**: rebuild snapshot from replay; investigate root cause.

**Mode 2: Idempotency-key collision.** Two unrelated commands happen to share an idempotency key (bug in caller). **Detection**: aggregate rejects the second with a clear "this idempotency key already produced a different event" error. **Recovery**: caller fixes their key generation; the second command is rejected; no data corruption.

**Mode 3: Concurrency conflict storm.** High contention on a single hot wallet (e.g., a popular tenant's main wallet). **Detection**: metrics on retry rate. **Recovery**: shard the wallet (multiple wallets per tenant with weighted routing) OR adopt pessimistic locking for that wallet; both are configurable.

**Mode 4: SaaS profit ledger drift.** Bug in accrual calculation drifts the ledger from what the tenant actually owes. **Detection**: daily reconciliation report comparing accrued vs computed-from-events. **Recovery**: roll back projections; recompute from events; settle by issuing manual credit/debit.

**Mode 5: Dormancy fee disputes.** Tenant changes maintenance fee policy; customer complains the new policy was applied to old tokens. **Detection**: customer support ticket. **Recovery**: pull the `FundingTermsSnapshotJson` from the relevant WalletFunded events to prove what policy was in effect at funding time. (This is why the snapshot is non-negotiable.)

**Mode 6: TenantPrefundedWallet suspension blocking legitimate tenant-customer activity.** Tenant's wallet hits zero; admin UI suspended; tenant operator can't fund. **Detection**: tenant calls support. **Recovery**: AppMasterAdmin issues a manual credit OR extends the grace period. The design explicitly keeps tenant-customer activity (purchases, customer-wallet funding) RUNNING during tenant admin suspension precisely to avoid punishing innocent customers.

## When to use this elsewhere

**Signs this pattern fits your project:**

- Building any prepaid-credit / loyalty-points / gift-card / SaaS-metered-billing system.
- Audit requirements are strict (financial / regulatory / contractual).
- Economic model is non-trivial (fees, splits, dormancy, refunds).
- Multi-tenancy is in play OR the SaaS itself needs to charge a cut of every transaction.
- You expect the system to live for 5+ years with significant economic-model evolution.

**Signs this pattern is overkill:**

- Simple "store a balance in a row" use case (no audit, no replay, no rebuild from truth).
- Single-tenant, single-currency, single-fee-mode use case.
- Short project lifetime where event-store schema management isn't worth the investment.

## Adaptation checklist

1. **Define your events with full economic-breakdown fields FROM DAY ONE.** Don't ship a v1 without them — adding them later requires up-casting every historical event.
2. **Pick your event-store backend based on your database.** Postgres → Marten; everything else → EF Core (or NoSQL provider via the storage abstraction).
3. **Author your aggregate with explicit invariant enforcement.** Every command method on the aggregate checks invariants before returning new events; never trust the caller.
4. **Build the MediatR pipeline with at least: Idempotency, Validation, Logging, Transaction behaviors.** These are foundation; everything else depends on them.
5. **Snapshot every N events.** Default 50; tune per backend (Cosmos needs aggressive snapshotting due to RU cost).
6. **Design fee-handling modes as tenant configuration, not platform configuration.** Tenants differ; the platform must accommodate.
7. **Pick ONE SaaS revenue settlement mode platform-wide.** Don't try to support all four simultaneously in your first release; pick the one that matches your business model and add the others as v1.x patches if needed.
8. **Implement dormancy hedging early.** Customer-acquisition retention is a top-3 KPI; the dormancy revenue stream materially affects unit economics for any prepaid product.
9. **Ship the recursive meta-tenant pattern (Option D) only if your tenants are willing to prepay.** It's an opinionated billing model that not all customers will accept; have a fallback (BundledMonthlyInvoice) for tenants who can't or won't prefund.
10. **Make every operation idempotent.** Network retries, webhook re-deliveries, partial-failure resumes — all assume idempotency. Bake it into the command DTOs (every command requires an `IdempotencyKey`).
11. **Run the projection rebuild in a controlled environment before production rollout.** Always.
12. **Build the SaaS profit ledger from day one even if you don't bill it monthly yet.** Switching from "compute on demand" to "accrue then bill" later requires backfilling the ledger from event history. Better to accrue from the start.

## Discussion / open questions

- **Should the wallet support multi-currency natively, or one currency per wallet?** Current design: one currency per wallet, locked at WalletOpened. Multi-currency would require either implicit FX conversion at every operation (complex) or wallet-to-wallet transfers (cleaner but more API surface).
- **How to handle FX rate changes for foreign currencies?** The token-to-currency ratio is locked per currency in the SaaS config. If the SaaS later changes the ratio for, say, JPY from "1 token = ¥1" to "1 token = ¥10", historical events use the old ratio (FundingTermsSnapshotJson captures this) but new events use the new ratio. Customer-facing UX should explain this transparently.
- **Should the wallet expose subscription billing primitives, or leave subscriptions to a separate system?** Current design: wallet supports subscription DEBITS (WalletDebited with `Target.Kind = "subscription_invoice"`); the subscription itself (cycle scheduling, prorating, etc.) lives elsewhere. Could be expanded if there's a real need.
- **Is there a fraud surface around "lots of small refund-as-credit requests" gaming the system?** Possibly — a customer who funds, never spends, and refunds repeatedly to harvest tokens. Detection should look for refund frequency anomalies. Worth instrumenting.

## Related patterns

- **Case Study 01 — Lift-and-Shift Architecture** — the wallet is the reference implementation of the lift-shift pattern; the patterns are mutually reinforcing.
- **Case Study 05 — Multi-Tenancy as Optional** — the wallet uses the mode-agnostic abstraction pattern (`IWalletIdentityProvider.IsMultiTenantMode`) so single-tenant hosts can use it without installing the multi-tenant infrastructure.
- **CQRS + Event Sourcing (Greg Young, Vaughn Vernon)** — the foundational pattern this case study builds on.
- **The Saga pattern** — long-running multi-step transactions; relevant if the wallet needs to coordinate with external systems (e.g., subscription cancellation that needs to refund the most recent debit).
- **The Outbox pattern** — relevant if the wallet needs to publish events to a downstream message bus reliably.

## Citation format

> Chipman, Mark. *Event-Sourced Wallet with Comprehensive Economic Modeling*. PolarSharp Architectural Case Study 02. Molls and Hersh, LLC, 2026. https://github.com/mollsandhersh/Polar.sh_Nuget/tree/main/Case%20Studies
