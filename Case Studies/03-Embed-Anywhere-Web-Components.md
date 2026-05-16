# Case Study 03 — Embed-Anywhere Web Components with Server-as-Source-of-Truth

> **Author**: Mark Chipman — Molls and Hersh, LLC.
> **Date**: 2026-05-15
> **Status**: Planned. Reference implementation: PolarSharp.EcommerceStorefronts.WebComponents v1.4 (in design).
> **License**: © Mark Chipman / Molls and Hersh, LLC. 2026. Educational use permitted with attribution.
> **Related files**: PolarSharp v1.4 plan, "Storefronts" section.

## TL;DR

A set of true Web Components (Custom Elements + Shadow DOM, built with Stencil for SCSS-friendly style scoping) is embeddable in ANY HTML page in ANY framework (React, Vue, Angular, plain HTML, Astro, WordPress, etc.) AND scoped per-tenant via dynamic embed keys validated at runtime by a shared SignalR hub. The components communicate with each other in real time via three layered mechanisms (DOM CustomEvents + shared pub-sub bus + shared SignalR connection). Crucially, the architecture treats the client purely as a VIEW — every state-mutating operation is authoritatively re-computed by the server, making the system fully resistant to client-side tampering (changing prices in dev tools, swapping product IDs, etc.) via 10 layered defenses. The pattern was inspired by SnipCart's embed model but moves it from a proprietary loader to a standards-based architecture, adds dynamic per-embed scoping with live updates, and bakes fraud prevention into the protocol from day one.

## Historical context / inspiration / prior art

**SnipCart** (founded 2013, acquired by DigitalOcean 2021) pioneered the "drop a JavaScript snippet on any website and instantly have ecommerce" model. Their loader fetches their backend's product catalog, renders carts in a side drawer, and handles checkout — all without the host site needing a CMS or backend integration. The model was revelatory: an HTML-only static site could become a full ecommerce store overnight. SnipCart's downside was the proprietary loader (closed implementation; you trust SnipCart's CDN; styling depended on their conventions).

**Shopify Buy Button** (2017) followed a similar model with a JS-driven loader that injects "Add to Cart" buttons into any website. Like SnipCart, it's a proprietary loader; styling is limited; data flow is one-way (you embed what Shopify already has in your store).

**Web Components** (Custom Elements v1 standardized 2016; widely adopted 2018-2020) are a W3C standard set of browser primitives — Custom Elements, Shadow DOM, HTML Templates, ES Modules — that let you create custom HTML elements with encapsulated styling and behavior. The standard is now baseline in all modern browsers, eliminating the need for proprietary loader frameworks. Component libraries built as Web Components work natively in React/Vue/Angular/plain HTML without per-framework adapters.

**Stencil.js** (Ionic Framework, 2018) is a compiler designed specifically for shipping distributable Web Components. It supports three shadow modes: `shadow: true` (full Shadow DOM with style encapsulation), `shadow: 'scoped'` (style scoping via attribute selectors, allowing external CSS to penetrate via specificity), and `shadow: false` (light DOM). The `'scoped'` mode is the sweet spot for embeddable components where external CSS authors need to override styling.

**SignalR** (Microsoft, 2013; .NET Core port 2016) provides real-time bidirectional client↔server communication over WebSockets (with fallback transports). The pattern of "browser embeds a Web Component; component connects to SignalR; server pushes updates" is straightforward but the GENERAL pattern of using SignalR to drive embedded Web Components on third-party websites is less common — most SignalR deployments push to first-party Blazor/JS apps, not embedded widgets.

The **server-as-source-of-truth** pattern for fraud prevention is universal in ecommerce — Stripe, Square, PayPal, SnipCart all do it — but PolarSharp's contribution is articulating the FULL set of layered defenses (10 distinct layers) as a coherent design pattern rather than an ad-hoc collection of mitigations. The pattern is documented here so it can be applied to ANY embeddable widget product (not just commerce — embeddable calendars, surveys, scheduling tools, etc., all face the same fraud surface).

## Problem

A SaaS platform wants its tenants to be able to sell their products on ANY website (their own main site, a partner's site, an influencer's microsite, a third-party blog) without requiring per-host backend integration. The classical solution is an iframe (works everywhere, isolates security, but is visually constrained and breaks responsive layouts). The modern solution is JavaScript loaders that inject DOM (more visual freedom but coupling to the loader's conventions). Neither is satisfactory: iframes are visually limited; JS loaders are proprietary.

Compounding the problem: every embedded component is running on a host page the SaaS does not control. A rogue actor can:
- Open browser dev tools and change displayed prices to $0.01
- Modify product IDs in the DOM to point to higher-tier products
- Replay add-to-cart requests with manipulated parameters
- Use a stolen embed key on their own site to scrape tenant data
- DDoS the SaaS by spamming requests through a published embed
- Inject malicious content via XSS if the host page is compromised

The SaaS must:
1. Render rich, responsive, themable widgets on ANY host page in ANY framework.
2. Support per-tenant, per-deployment scoping so the same widget configured differently can appear on different sites.
3. Allow tenants to author their own SCSS overrides for branding without losing widget integrity.
4. Provide cross-widget real-time communication (cart updates in `<polar-product-card>` reflect immediately in `<polar-mini-cart>` in the header).
5. Be fraud-resistant by design — no client-supplied price or scope is trusted.
6. Survive deployment on host pages with hostile or compromised security postures.
7. Be lift-shift portable (the WC layer can become its own product separate from the parent SaaS).

## Forces / constraints

1. **Distribution channel matters.** WC must work via npm install (for hosts with build pipelines), CDN script tag (for plain HTML / WordPress / Squarespace), AND a NuGet bundle (for .NET hosts who want self-contained deployment). Three distribution paths for the same compiled bundle.
2. **Style isolation vs theming flexibility.** Pure Shadow DOM is the strongest encapsulation but blocks external SCSS overrides. Pure light DOM allows SCSS but loses encapsulation. The compromise must satisfy both.
3. **Per-embed configuration must update live.** When a tenant changes a widget's config in their admin UI, every live embed of that widget across every host page should re-render with the new config WITHOUT requiring a page refresh from the customer.
4. **Cross-widget communication must work even when widgets are loaded asynchronously, render in different orders, or are dynamically added/removed.** No assumption that all widgets are present on initial page load.
5. **Server-as-source-of-truth means EVERY state mutation goes through the server.** Adding a stale "client thinks the price is $5" optimization breaks the fraud-prevention model. No client-side authority allowed.
6. **The system must scale to thousands of concurrent customers per tenant.** Per-embed SignalR connections, per-tenant connection pools, fair queuing.
7. **The architecture must be technology-stable for 5+ years.** Web Components, Stencil, SignalR, JSON are all mature standards. Avoid bleeding-edge technologies that might disappear.

## The pattern

### Layer 1: True Web Components built with Stencil

```
@Component({
  tag: 'polar-product-card',
  styleUrl: 'product-card.scss',
  shadow: 'scoped',           // ← the key choice: scoped style isolation, NOT full shadow DOM
})
export class PolarProductCard {
  @Prop() embedKey!: string;
  @Prop() productId!: string;
  @State() config!: WidgetConfig;
  @State() product!: ProductSnapshot;

  async connectedCallback() {
    const conn = await PolarSharedConnection.getOrCreate({
      hubUrl: this.dataset.hubUrl ?? 'https://hub.polarsharp.io/wc',
      embedKey: this.embedKey,
    });
    this.config = await conn.invoke('GetEmbedConfig', this.embedKey);
    this.product = await conn.invoke('GetProductSnapshot', { embedKey: this.embedKey, productId: this.productId });
    conn.on('ProductUpdated', this.onProductUpdated);
    conn.on('CartUpdated', this.onCartUpdated);
  }

  render() {
    // Renders based on this.config (display mode, fields shown, etc.) + this.product
  }
}
```

The `shadow: 'scoped'` choice is critical. It gives style scoping (the WC's internal classes are namespaced via attribute selectors and don't leak out) WHILE allowing external CSS (from the host page's Tailwind / Bootstrap / Materialize / Bulma / custom SCSS) to penetrate via normal specificity rules.

### Layer 2: Dynamic per-embed scoping via signed embed keys

Tenant generates an embed key in the admin UI:

```
ek_<env>_<tenant_slug>_<random>
ek_prod_acme_a1b2c3d4e5f6
```

The key is:
- **Public** — lives in the HTML source; cannot leak secrets.
- **HMAC-signed** — the server detects tampering.
- **Scope-bearing** — maps to a `wc_embed_configurations` row that specifies WHICH products / categories / actions this embed can access.
- **Origin-scoped** — `allowed_origins_json` enumerates which hostnames are permitted to use the key.
- **Rate-limited** — per-embed rate limits prevent abuse.
- **Revocable** — tenant can deactivate keys; auto-revoke after fraud-strike threshold.

```sql
wc_embed_configurations
  id                       UNIQUEIDENTIFIER PK
  embed_key                NVARCHAR(64) UNIQUE
  tenant_id                UNIQUEIDENTIFIER FK
  wc_type                  NVARCHAR(64)      -- "product-card" | "product-grid" | "mini-cart" | ...
  scope_json               NVARCHAR(MAX)     -- allowed productIds, categoryIds, max quantities, etc.
  display_config_json      NVARCHAR(MAX)     -- view mode, columns, features shown, locale, preset
  allowed_origins_json     NVARCHAR(MAX)     -- CORS enforcement
  rate_limit_per_minute    INT
  hmac_signature           NVARCHAR(128)     -- tamper detection
  is_active                BIT
  fraud_strike_count       INT               -- auto-revoke at threshold
  ...
```

### Layer 3: Shared SignalR connection with embed-key validation

On `connectedCallback`, the WC opens (or joins) a shared SignalR connection. The hub validates:
1. HMAC signature matches
2. `is_active = true` and not revoked / expired
3. Request's `Origin` header is in `allowed_origins_json`
4. Rate limit OK
5. Connection joins the tenant's SignalR group for live updates

All WCs on the same page sharing the same embed key share the SAME SignalR connection (de-duplicated by embed key). This minimizes connection overhead.

### Layer 4: Three-layer real-time communication

Web Components on the same page need to coordinate. Three patterns layered together:

```
┌─────────────────────────────────────────────────────────────────────┐
│ Layer A: DOM CustomEvent                                            │
│   For browser-native interactions ("modal opened", "button clicked")│
│   - Events bubble; sibling WCs listen on common ancestor or document│
│   - Standard tech; no library                                        │
│                                                                     │
│ Layer B: Shared pub-sub bus (page-internal data flow)               │
│   For "cart updated locally", "filter changed", etc.                │
│   - polarBus.emit('cart.updated', data)                             │
│   - Namespaced per embed key                                        │
│                                                                     │
│ Layer C: Shared SignalR connection (server-state changes)           │
│   For "inventory low", "price changed by tenant admin",             │
│   "another browser session added to same cart"                      │
│   - Real-time across sessions; server-pushed                        │
└─────────────────────────────────────────────────────────────────────┘
```

The shared infrastructure (`@polarsharp/web-components-shared` npm module) manages all three transparently. Each WC auto-wires on `connectedCallback`.

### Layer 5: 10-layer fraud prevention (server-as-source-of-truth)

The complete defense set:

| # | Layer | What it prevents |
|---|---|---|
| 1 | Cart operations send identifiers only — never prices/totals | DOM-edited prices being submitted as authoritative |
| 2 | Embed-key scope enforcement on every request | Tampered productId pointing outside allowed scope |
| 3 | Origin validation at SignalR handshake | Rogue site embedding stolen embed key |
| 4 | Quantity + min-order enforcement server-side | Negative / zero / over-limit quantities |
| 5 | Wallet balance verification on checkout | Customer attempting to purchase without sufficient prepaid balance |
| 6 | Cart hash for offline-tampering detection | In-memory cart-object modification between requests |
| 7 | Rate limiting per embed-key + per source IP | DDoS / brute-force / scraping |
| 8 | Fraud-strike accumulation with auto-revoke | Persistent attackers |
| 9 | Audit trail for all fraud-detected operations | Forensic investigation; pattern detection |
| 10 | Customer-visible "prices have changed" UX at checkout | Confusing UX from stale cart contents |

### Layer 6: Framework-preset CSS theming

Every WC exposes CSS Custom Properties for design tokens:

```css
polar-product-card {
  --polar-card-bg: ...;
  --polar-card-text: ...;
  --polar-card-padding: ...;
  --polar-card-border-radius: ...;
  --polar-card-shadow: ...;
  /* 30-50 tokens per component */
}
```

Preset CSS files map the tokens to popular framework defaults:
```
@polarsharp/web-components-theme-tailwind.css
@polarsharp/web-components-theme-bootstrap.css
@polarsharp/web-components-theme-materialize.css
@polarsharp/web-components-theme-bulma.css
@polarsharp/web-components-theme-unstyled.css
```

Tenant picks one + optionally overrides individual tokens via SCSS for fine-tuning. Four levels of customization with progressively more effort: pick preset → override tokens inline → author SCSS overrides → write custom preset.

### Layer 7: Live config updates

When tenant changes config in admin UI:
1. Server updates the `wc_embed_configurations` row.
2. Server pushes `EmbedConfigUpdated` event to all live SignalR connections in the tenant's group.
3. Every WC instance using that embed key re-fetches its config and re-renders.

No page refresh needed; no JS bundle re-download needed. The WC bundle is static; only the config is dynamic.

## Implementation mechanics

### Step 1: Choose the WC build tool (Stencil; locked)

Stencil.js for the reasons covered in the historical context — `shadow: 'scoped'` mode is purpose-built for embeddable components, TypeScript-first authoring, AOT compilation, smallest runtime, designed for shipping distributable Web Components.

### Step 2: Define the WC types + their config schemas

```
polar-product-card        — single product, multiple display modes
polar-product-grid        — paginated grid with search/filter/sort
polar-product-detail      — full product detail with variant selector
polar-mini-cart           — header cart toggler with item count
polar-cart-drawer         — slide-out full cart view
polar-checkout-button     — initiates checkout flow
polar-checkout-page       — full checkout form with payment
polar-account-menu        — customer profile menu
polar-login-button        — initiates customer auth flow
polar-order-history-list  — paginated past orders
polar-wallet-balance      — displays current wallet balance
polar-wallet-topup-flow   — wallet funding UI
polar-storefront-script   — Razor TagHelper for easy MVC embedding
```

Each WC type has a config schema (JSON Schema documented) listing valid options. Tenant admin UI surfaces the config schema for guided embed creation.

### Step 3: Build the shared infrastructure module

`@polarsharp/web-components-shared` provides:
- `PolarSharedConnection` — singleton SignalR connection per (hubUrl, embedKey) pair
- `polarBus` — pub-sub bus for in-page WC-to-WC events
- Standard event-name constants (avoid string-typing collisions across WCs)

### Step 4: Build the SignalR hub on the .NET side

```csharp
public class PolarWebComponentHub : Hub
{
    public async Task<WidgetConfig> GetEmbedConfig(string embedKey) { ... }
    public async Task<ProductSnapshot> GetProductSnapshot(GetProductRequest req) { ... }
    public async Task<Cart> AddToCart(AddToCartRequest req) { ... }
    public async Task<Cart> RemoveFromCart(RemoveFromCartRequest req) { ... }
    public async Task<Cart> ApplyDiscount(ApplyDiscountRequest req) { ... }
    public async Task<CheckoutSession> StartCheckout(StartCheckoutRequest req) { ... }
    // ...
}
```

Every method validates the embed key first, then enforces the scope rules, then performs the operation, then returns the server-computed result to push to all subscribed clients.

### Step 5: Build the admin UI for embed-key management

Razor Class Library (Blazor or Razor Pages) lets tenant operators:
- Create new embeds (pick WC type, configure scope, configure display, set allowed origins, set rate limit)
- View existing embeds (status, recent activity, fraud-strike count)
- Edit live embed config (changes push to live customers via SignalR)
- Revoke / suspend embeds
- View fraud audit log
- Generate embed snippet for copy-paste into their host site

### Step 6: Distribute via both NuGet and npm

Compiled bundle ships as:
- Static web assets in `PolarSharp.EcommerceStorefronts.WebComponents` NuGet (under `wwwroot/`)
- npm package `@polarsharp/web-components` for non-.NET hosts
- Same JS bundle in both channels

CDN delivery is the third path: hosts can link to `https://cdn.polarsharp.io/storefronts/v1.4/polar-storefront-webcomponents.js` directly.

## Worked example (from PolarSharp)

The PolarSharp.EcommerceStorefronts.WebComponents v1.4 implementation:

- **Stencil-compiled bundle** (~150KB minified + gzipped for all 13 WC types combined)
- **5 framework preset CSS files** for Tailwind / Bootstrap / Materialize / Bulma / unstyled
- **3 distribution channels**: NuGet bundle, npm package, CDN
- **SignalR hub** on the .NET side with embed-key validation + scope enforcement
- **Admin RCL** (Blazor) for tenant embed-key management
- **3 npm packages**: `@polarsharp/web-components`, `@polarsharp/web-components-shared`, `@polarsharp/web-components-theme-*` (5 preset packages)
- **4 .NET NuGet packages**: WebComponents (bundle), WebComponents.SignalRHub (.NET hub), WebComponents.Admin (admin RCL), Polar.WebComponents (PolarSharp bridge wiring catalog/wallet/identity into the hub)
- **Per-embed `wc_embed_configurations`** table; per-fraud `wc_fraud_attempts` table

Example tenant deployment:

```html
<script src="https://cdn.polarsharp.io/storefronts/v1.4/polar-storefront-webcomponents.js"></script>
<link rel="stylesheet" href="https://cdn.polarsharp.io/storefronts/v1.4/theme-tailwind.css" />

<header>
  <polar-mini-cart embed-key="ek_prod_acme_a1b2c3d4e5f6"></polar-mini-cart>
</header>

<main>
  <polar-product-grid embed-key="ek_prod_acme_g1h2i3j4k5" category="t-shirts" page-size="12"></polar-product-grid>
</main>

<footer>
  <polar-checkout-button embed-key="ek_prod_acme_a1b2c3d4e5f6"></polar-checkout-button>
</footer>
```

The customer browses, adds to cart in the grid, watches the header mini-cart update in real time, clicks checkout, completes purchase. Same WC bundle works on Acme's main site, their landing page, and a partner blog — all with different embed keys configuring different scopes.

## Trade-offs

**What you give up:**

1. **First-load latency.** WCs must fetch config from the SignalR hub before rendering — there's a brief "skeleton" state on first visit. Mitigations: server-render the WC's static skeleton via Razor TagHelper if the host is .NET; aggressive CDN caching of the JS bundle.
2. **Bundle size.** 150KB for all 13 WC types combined is reasonable but not negligible. Per-WC dynamic imports (load only what's on the page) can reduce this.
3. **Real-time means resource cost.** Per-connection SignalR overhead at scale (thousands of concurrent customers per tenant) needs infrastructure planning. Azure SignalR Service or self-hosted SignalR scale-out via Redis backplane.
4. **Shadow DOM theming has limits.** `shadow: 'scoped'` is the compromise between encapsulation and theming flexibility; pure-Shadow-DOM solutions and pure-light-DOM solutions both have edge cases the compromise doesn't perfectly address.
5. **Admin complexity for tenants.** Managing multiple embed keys for multiple sites adds operational burden. The admin UI must be excellent.

**Alternative patterns and why this one over those:**

- **iframes** — work everywhere; visually constrained; hard to integrate with host page CSS; no cross-iframe communication without postMessage gymnastics. Use for high-security checkout step where iframe isolation is a feature.
- **Proprietary JS loader (SnipCart model)** — works; coupling to your loader; you own all the maintenance. Web Components standardize the contract.
- **Per-framework SDKs (React SDK + Vue SDK + Angular SDK)** — best DX per framework; 3x the maintenance; doesn't cover hosts in frameworks you didn't ship an SDK for. Web Components work everywhere.

## Failure modes

**Mode 1: Stolen embed key.** Attacker copies an embed key from a tenant's public site and uses it on their own site. **Detection**: Origin header doesn't match `allowed_origins_json`; SignalR handshake fails. **Recovery**: tenant adds the attacker's origin OR (more likely) auto-block continues.

**Mode 2: Embed key with overly-permissive scope.** Tenant generated an embed key with `scope.productIds = ['*']` (all products); the embed is used on a partner site that should only sell a subset. **Detection**: tenant operator reviews the embed config; audit log shows products being accessed outside the partner's expected set. **Recovery**: tenant generates a new scoped embed key; revokes the over-permissive one.

**Mode 3: Live config update doesn't propagate.** Tenant changes config; some customers' WCs don't re-render. **Detection**: customer support; server-side metric on `EmbedConfigUpdated` push count vs ack count. **Recovery**: customer refreshes the page; investigate why their SignalR connection dropped silently (network blip, browser sleep, etc.).

**Mode 4: Cart hash mismatch on legitimate cart.** User has two browser tabs open with the same cart; one tab's mutations update the cart-hash; the other tab's next request has a stale hash. **Detection**: server rejects the stale-hash request. **Recovery**: the rejected request returns the current cart state; the affected tab re-syncs and retries the original action.

**Mode 5: Bundle size regression.** A new WC version adds 50KB without anyone noticing. **Detection**: CI bundle-size budget; fail the build if total bundle exceeds threshold. **Recovery**: identify the regression; lazy-load if appropriate; refactor.

**Mode 6: SignalR connection pool exhaustion.** Tenant's customer base grows; single SignalR backplane can't keep up. **Detection**: SignalR backplane metrics; connection-rejection logs. **Recovery**: Azure SignalR Service auto-scale; or Redis backplane scale-out for self-hosted.

## When to use this elsewhere

**Signs this pattern fits your project:**

- Building any embeddable widget product (commerce, calendars, surveys, scheduling, dashboards, polls, contact forms, etc.).
- Tenants need multi-channel selling (one product catalog appearing on multiple sites simultaneously).
- Real-time updates matter (live inventory, live prices, multi-tab cart sync).
- Hosts span multiple frameworks (React, Vue, Angular, plain HTML, etc.).
- Fraud resistance is non-optional.
- You want to avoid building per-framework SDKs.

**Signs this pattern is overkill:**

- Single-host deployment (your widgets only ever appear on your own first-party site → use Blazor / React directly).
- No real-time updates needed (static product info that doesn't change → simple JS SDK is fine).
- Small tenant scale (10s of tenants, not 1000s) where the SignalR infrastructure cost outweighs the benefit.
- Fully trusted host pages (intranet deployments) where fraud prevention is irrelevant.

## Adaptation checklist

1. **Pick your WC framework.** Stencil is recommended for new projects of this scope. Lit is fine for simpler cases. Vue / React-as-WC compilers are not recommended for embed-anywhere use cases (bundle bloat).
2. **Design your embed-key schema.** Decide what to encode in the key vs what to look up server-side. Public-safe + signed + scope-bearing is the right baseline.
3. **Build your SignalR hub FIRST.** Before any WC, build the hub with `GetEmbedConfig`, `GetProductSnapshot` (or your equivalent), `AddToCart`. Validate the embed-key flow end-to-end.
4. **Build ONE WC end-to-end** before building all of them. Pick the simplest (product card) and walk it through: render skeleton → connect SignalR → fetch config → fetch product → render full → handle add-to-cart → propagate to mini cart. Get this right before scaling.
5. **Author the framework-preset CSS files** for your 3-5 target frameworks. Tailwind first (most popular in 2026); add Bootstrap and one CSS-only framework next.
6. **Build the admin UI for embed-key management** before launching to tenants. Tenants need a way to create, view, edit, revoke keys + see audit logs.
7. **Ship the 10-layer fraud-prevention from day one.** Adding fraud prevention later is harder than building it in; the design has to be fraud-aware throughout.
8. **Document the embed snippet format prominently.** Tenants copy-paste this into their HTML; the snippet is your product's customer-facing surface. Make it bulletproof.
9. **Distribute via CDN AND npm AND NuGet.** CDN covers plain-HTML hosts; npm covers JS-build hosts; NuGet covers .NET hosts. All three matter.
10. **Plan for SignalR scale from day one.** Test with realistic concurrent-connection counts. Plan the backplane (Azure SignalR Service or Redis) before you need it.
11. **Build the live-config-update flow early.** Tenants who see "I changed the config and my live customers updated instantly" become enthusiastic adopters. This is a wow moment.
12. **Build bundle-size CI gates.** Set a budget; fail builds that exceed it. Without this, the bundle bloats over time.

## Discussion / open questions

- **Should embed keys expire by default?** Forcing periodic rotation reduces stolen-key risk but adds operational burden. Default null-expiry with optional `ExpiresAt` is the current design; revisit if abuse patterns suggest forced rotation would help.
- **How to handle WC pre-rendering for SEO?** SignalR-dependent WCs render empty on first SSR pass (config not fetched yet); SEO crawlers see empty divs. Possible mitigation: server-rendered product cards via Razor TagHelper in .NET hosts; static rendering of the most-SEO-critical WCs at build time for non-.NET hosts; a separate `data-ssr-snapshot` attribute carrying server-rendered content for crawlers.
- **Should WCs work offline?** Service Worker + cached config + IndexedDB cart could enable offline browsing of products. Materially complex; consider only if tenants need progressive-web-app capability.
- **Authentication for the customer.** The WC needs to know "who is this customer" for cart persistence + checkout. Options: (a) the host page passes a customer token via attribute; (b) the WC handles auth itself with a built-in login flow; (c) the WC defers to the host's session cookie if same-origin. PolarSharp uses (b) for embed-anywhere flexibility but supports (c) when same-origin.
- **What about AMP / accelerated mobile pages?** AMP has its own restricted WC subset (`amp-`-prefixed elements). WC bundles intended for AMP need stricter constraints; out of scope for v1.4 but worth a future case study.

## Related patterns

- **Case Study 01 — Lift-and-Shift Architecture** — the WC layer is structured per the lift-shift pattern so it can become its own standalone product separate from the parent PolarSharp SDK.
- **Case Study 02 — Event-Sourced Wallet** — the WC checkout flow integrates with the wallet (display balance, top-up flow, debit on checkout); the server-as-source-of-truth pattern protects both.
- **The CSP (Content Security Policy) pattern** — embedded WCs running on tenant pages may interact with the host page's CSP; documentation should cover required CSP allowances.
- **WebSocket vs SSE vs polling** for real-time updates — SignalR abstracts these; the design pattern of "shared real-time connection across embedded widgets on one page" applies regardless of transport.

## Citation format

> Chipman, Mark. *Embed-Anywhere Web Components with Server-as-Source-of-Truth*. PolarSharp Architectural Case Study 03. Molls and Hersh, LLC, 2026. https://github.com/mollsandhersh/Polar.sh_Nuget/tree/main/Case%20Studies
