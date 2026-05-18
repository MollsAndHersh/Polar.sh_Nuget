# Choosing your PolarSharp DI wiring

> When you install PolarSharp's NuGet packages and then look at your application's startup file, you might wonder: *do I need to wire each one up by hand? What about that "MediatR" thing — if more than one PolarSharp package uses it, do they fight each other? What happens if I install a package but forget to flip it on?* This narrative walks through the three most common deployment shapes — minimum, middle, and full — so you can decide what your application's startup file should look like, in plain language.

## What this narrative is about (and what it isn't)

This narrative is about **wiring** — the handful of lines in your application's startup file (in .NET this is usually called `Program.cs`) that say *"use this PolarSharp feature."*

It is **not** about what each feature actually does. Those stories live in other narratives (about onboarding, payouts, the prepaid wallet, and so on) and in the per-package reference articles.

It is **not** about configuration values, either. Things like access tokens, the location of the SQLite directory, or which email provider to use for tenant notifications live in `appsettings.json`. The Configuration article covers those exhaustively.

This one is purely about the *plumbing* — the question of *"which features do I turn on, and in what order do I call them?"*

## A quick mental model: installing vs. activating

Here is the most important idea in this whole narrative, and it trips up almost everyone the first time they meet it.

**Installing a package is not the same as activating it.**

Think of it like buying a power tool from the hardware store. You bring the drill home, you put it on your workbench, and it sits there. You own it. It is in your house. But until you plug it in and pull the trigger, it does not actually drill anything.

PolarSharp packages work the same way. When you run `dotnet add package PolarSharp.MultiTenant.Notifications`, you are putting the drill on the workbench. The DLL ends up in your application's output folder. The classes inside it are technically *available* to your code. But unless you also call the matching `services.AddPolarMultiTenantNotifications(...)` line in your startup file, your application *does not actually do anything with it*. No emails will be sent. No SMS messages will go out. The package is installed but never activated.

There is a friendly way to think about the activation step too: **calling `services.AddPolarSomething(...)` is like ordering off a menu.** You point at the items you want; the kitchen (PolarSharp) takes care of all the prep work behind the scenes. You do not need to know what pans they use, how long things simmer, or what order they fire the dishes. You just order what you want.

Every PolarSharp package that needs activation publishes its own one-line "menu item" — an extension method whose name starts with `AddPolar...`. Pick the ones you want; skip the ones you do not.

Now let us walk through the three most common orders.

## Scenario 1: PolarSharp.MultiTenant + tenant lifecycle, nothing else

Suppose you are building a SaaS — software that several different companies will use as their own private workspace. You have a handful of customers ("tenants") today. You want:

- Each tenant to have its own isolated access to Polar.sh.
- The ability to suspend a tenant who has not paid, reactivate them when they catch up, and eventually delete tenants who have gone away.
- A boring, hosted database — say SQL Server in the cloud, or Postgres on a managed service — for your tenant registry.

You do **not** yet want prepaid wallets. You do **not** yet need email notifications when tenant status changes. You are not using SQLite-on-disk, so you do not need Litestream replication either.

This is the minimum viable PolarSharp install, and it is shorter than you might think.

```csharp
// Program.cs — Scenario 1: minimum multi-tenant setup
var builder = WebApplication.CreateBuilder(args);

builder.Services
    // The core wiring. Sets up the PolarClient + HttpClient pool + everything
    // PolarSharp needs to talk to Polar.sh in the first place.
    .AddPolarInfrastructure(builder.Configuration)
    // Teaches the application about tenants — who they are, how to identify
    // the current request's tenant from a header / route / claim / hostname,
    // and how to hand back the right PolarClient for that tenant.
    .AddPolarMultiTenant()
    // Adds ITenantStatusService so your code can call SuspendAsync /
    // ReactivateAsync / DeactivateAsync / DeleteAsync. Also publishes
    // TenantStatusChangedNotification events whenever those happen.
    .AddPolarTenantLifecycle(builder.Configuration);

var app = builder.Build();
app.UsePolarInfrastructure();   // this calls UseMultiTenant() for you
app.Run();
```

That is the whole setup. Three extension method calls.

Notice what is missing: there is no `services.AddMediatR(...)` line. The lifecycle service uses something called MediatR (a small library that lets one part of the application "publish" an event and lets other parts "subscribe" to it without those two parts having to know about each other directly). But you do not have to register MediatR yourself — `AddPolarTenantLifecycle` quietly does it for you, behind the scenes, the moment you call it.

This is the menu-ordering style at work: you ordered tenant lifecycle; the kitchen handled the prep.

If your application calls `tenantStatusService.SuspendAsync(...)`, that lifecycle service publishes a `TenantStatusChangedNotification` event. Nobody is subscribed to listen for it yet — we did not install the notification dispatcher in this scenario — so the event is published into the void and nothing happens. That is fine! Your tenant is suspended (the database is updated), and the notification just had no listeners. If you add subscribers later (Scenario 2 or 3), they will start receiving the event automatically. No code change needed to the lifecycle service itself.

## Scenario 2: add the prepaid wallet

Now suppose your SaaS has grown a little. You want to start charging tenants in advance via a prepaid wallet — they top up a balance, and that balance gets spent down as they consume your service. PolarSharp ships an event-sourced wallet you can drop in for this.

You install the wallet packages:

```bash
dotnet add package PolarSharp.PrepaidWallets
dotnet add package PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.Sqlite   # or whichever backend
```

Then you adjust your startup file:

```csharp
// Program.cs — Scenario 2: Scenario 1 + prepaid wallet
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant()
    .AddPolarTenantLifecycle(builder.Configuration)
    // NEW: the prepaid wallet feature. Adds the Wallet aggregate, its command
    // handlers, projections, and the MediatR registrations for THIS package.
    .AddPolarPrepaidWallets();

var app = builder.Build();
app.UsePolarInfrastructure();
app.Run();
```

One new extension call. Done.

But wait — now we have **two** extensions both calling `AddMediatR` behind the scenes. `AddPolarTenantLifecycle` did it. `AddPolarPrepaidWallets` does it too. Surely that is a problem? Won't they conflict? Won't the second call overwrite the first?

No. And here is the analogy that makes it click: **MediatR de-duplication is a carpool.**

Imagine four coworkers all want a ride to the same destination. Each of them independently asks the rideshare app for a car. The rideshare app is smart enough to notice they are all going to the same place, so it sends *one* car that picks up all four of them. Each rider thought they were getting their own ride; what they got was a shared ride. Everyone arrives. Nobody is confused.

MediatR works the same way. When `AddPolarTenantLifecycle` calls `services.AddMediatR(...)`, MediatR registers itself in your DI container (the central registry of "what services exist in this application") and notes which assembly's handlers should be discovered. When `AddPolarPrepaidWallets` calls `services.AddMediatR(...)` a moment later, MediatR notices it has already been registered. It does *not* register itself twice. It just adds the wallet package's assembly to the list of places to look for handlers. One MediatR; two passengers; everybody arrives.

The practical result: in Scenario 2, when your code calls `tenantStatusService.SuspendAsync(...)`, the lifecycle service publishes its event. Right now nothing is subscribed to it from the wallet side either, so still nothing extra happens. But MediatR is set up correctly to fan out events to multiple subscribers from multiple packages — we just haven't added a subscriber yet. The pipes are ready; the water just isn't flowing yet.

## Scenario 3: add notifications + Litestream + everything

This is the full deployment. You want:

- Everything from Scenario 2.
- **Emails (and optionally SMS or webhooks) to the tenant's site manager** when their account is suspended, reactivated, deactivated, or deleted — so they actually find out something happened instead of just being mysteriously locked out.
- **SQLite-on-disk** as the tenant store, so the application runs entirely on a single machine without any external database server.
- **Litestream backup** — Litestream is a clever little program that continuously copies your SQLite file up to cloud object storage (S3, Backblaze B2, that sort of thing), so if the machine dies you have a recent backup. PolarSharp can automatically regenerate the Litestream configuration file whenever a tenant is suspended (to stop replicating their data) or reactivated (to resume).

The startup file:

```csharp
// Program.cs — Scenario 3: the full deployment
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant()
    .AddPolarTenantLifecycle(builder.Configuration)
    .AddPolarPrepaidWallets()
    // NEW: the notification dispatcher. Listens for TenantStatusChangedNotification
    // events and sends out emails / SMS / webhooks according to your appsettings.
    .AddPolarMultiTenantNotifications(builder.Configuration);

// The SQLite provider with Litestream support is wired through the builder's
// UseSqlite() call. enableLitestream defaults to true; the actual Litestream
// behavior is then gated on the UseLitestream config flag (see Things to know).
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant()
    .UseSqlite("/var/lib/polarsharp/tenants/");
//          ^ this single call:
//            - registers the SQLite tenant store
//            - registers the single-tenant -> multi-tenant upgrade migrator
//            - registers the Litestream services (which self-disable
//              if you have UseLitestream = false in appsettings)

var app = builder.Build();
app.UsePolarInfrastructure();
app.Run();
```

You now have several extensions all asking for MediatR behind the scenes. The lifecycle service from Scenario 1 publishes a `TenantStatusChangedNotification` whenever a tenant's status changes. The notification dispatcher (added by `AddPolarMultiTenantNotifications`) subscribes to that event and sends out emails. The Litestream auto-regenerator (added by `UseSqlite` when Litestream is enabled) *also* subscribes to that same event and regenerates the replication config.

Both subscribers receive the same event. Neither knows the other exists. Neither cares. MediatR fans it out to all subscribers automatically.

Your code calls one method:

```csharp
await tenantStatusService.SuspendAsync(tenantId, reason, ct);
```

…and as a result:

1. The tenant's status is updated in the registry.
2. The notification dispatcher sends an email (and maybe SMS / webhook) to the site manager.
3. The Litestream config is regenerated to exclude this tenant's database file from replication.

You did nothing special in your startup file to make those fan-outs happen. The extensions you called did all the wiring for you.

## What happens when you forget an extension

This is the most common mistake, and it is exactly the one the hardware-store analogy is designed to prevent.

You install a package — `dotnet add package PolarSharp.MultiTenant.Notifications` — and you think, *"great, now my tenants will get emails when their status changes."* Then you go to lunch and forget to add the `AddPolarMultiTenantNotifications(builder.Configuration)` line to your startup file.

Think of it like **putting groceries in your shopping cart but never actually checking out**. The groceries are in the cart. You see them sitting there. But you never paid, you never bagged them, you never took them home. They are not in your kitchen.

The package is on your workbench (in your `bin/` folder). The classes exist. But because you never *called the activation method*, your application never asked PolarSharp's DI to set up the notification dispatcher. No emails will ever go out. And PolarSharp will not yell at you about this — it has no way of knowing whether you *meant* to enable notifications or whether you are deliberately running without them (for example, during local development, or in a deployment where the tenant operator handles communication manually).

Here is what each "forgotten extension" actually means, in concrete terms:

- **Forget `AddPolarTenantLifecycle`.** `ITenantStatusService` is not registered, so you cannot inject it. If your code tries, the DI container will throw an error at startup telling you the service is missing. Nothing publishes lifecycle events. Even if you have notifications or Litestream installed, they cannot do anything, because nothing is firing the events they would listen to.

- **Forget `AddPolarMultiTenantNotifications`.** Lifecycle events fire (assuming you remembered `AddPolarTenantLifecycle`), but the email / SMS / webhook dispatcher is not subscribed. Tenants are silently suspended without ever being told. They will find out when they try to log in.

- **Forget `AddPolarPrepaidWallets`.** The Wallet aggregate, its command handlers, and its projections are not registered. Anything in your code that tries to inject `IWalletService` or one of the wallet command handlers will fail at startup with a missing-service error.

- **Forget the Litestream extension (or set `UseLitestream = false` in appsettings).** No Litestream config is generated. No replication runs. If the machine dies, you have whatever backup you set up by hand — which might be nothing. Importantly, tenant suspensions do *not* update the Litestream config to stop replicating that tenant's data. If you are using Litestream to comply with a contractual "stop processing this tenant's data" obligation, this matters.

The general design philosophy here is **quiet by default**. PolarSharp does not bark at you when a package is installed but not activated, because the difference between "you forgot" and "you deliberately chose not to" is genuinely ambiguous from PolarSharp's point of view. The trade-off: if you *thought* something was wired up and it is not, you might not notice until you go looking.

The simplest defense against this: **after each new package install, immediately add the corresponding extension call.** Treat it as a single two-step move, not two separate decisions. If you find yourself thinking *"I'll wire it up later,"* write it down somewhere.

## The MediatR de-duplication question, in a little more detail (but still plain)

The "carpool" framing above is the short version. Here is the slightly-longer version, for readers who want to be sure they understand why this works.

MediatR is what software people call a *mediator library*. The job it does is small but useful: it lets one part of your application announce that something happened, and lets other parts of the application listen for those announcements. The announcer and the listener never have to know about each other directly. They both just know about MediatR.

In PolarSharp, the **announcer** is the tenant lifecycle service. Whenever a tenant's status changes — suspended, reactivated, deactivated, deleted — it announces that fact by publishing a `TenantStatusChangedNotification`.

The **listeners** are scattered across other PolarSharp packages: the notification dispatcher listens so it can send an email; the Litestream regenerator listens so it can update the backup config; future packages will be able to listen too.

For all of this to work, MediatR has to be registered in your DI container at startup, and it has to know which assemblies (compiled DLL files) to look in for handlers. Each PolarSharp package that needs MediatR includes the registration call in its own activation extension. So:

- `AddPolarTenantLifecycle` calls `services.AddMediatR(...)` and tells it to scan the `PolarSharp.MultiTenant` assembly.
- `AddPolarMultiTenantNotifications` calls `services.AddMediatR(...)` and tells it to scan the `PolarSharp.MultiTenant.Notifications` assembly.
- `AddPolarPrepaidWallets` calls `services.AddMediatR(...)` and tells it to scan the `PolarSharp.PrepaidWallets` assembly.
- The Litestream extension calls `services.AddMediatR(...)` and tells it to scan its own assembly.

Each call is from a different package. Each cares about a different assembly. None of them coordinates with the others — they cannot, they were written separately.

MediatR's `AddMediatR` is specifically designed for this exact situation. It checks whether the core MediatR services have already been registered; if so, it does not re-register them. Then it takes the assembly you passed in and adds it to the pool of assemblies that get scanned for handlers. The result, after all four calls have run, is *one* MediatR instance with *four* assemblies in its handler-discovery set.

Your application code does not have to think about any of this. When you publish a notification, MediatR looks across all four assemblies' handlers, finds everyone subscribed to that notification type, and calls each of them. The same way the carpool driver picks up all four passengers and drops them off.

This is also why **adding a future PolarSharp package that uses MediatR will not break anything**. The pattern keeps composing. You will just call one more `AddPolarSomethingElse()` line and the new package's handlers will join the existing ones.

## Quick reference: package -> extension method -> what it gives you

| Package | Extension call | What you gain |
|---|---|---|
| `PolarSharp` | `services.AddPolarInfrastructure(config)` | The core `PolarClient` + HttpClient pool + everything PolarSharp needs to talk to Polar.sh at all. Required by every scenario. |
| `PolarSharp.MultiTenant` | `services.AddPolarMultiTenant()` | Per-tenant configuration + the Finbuckle.MultiTenant integration that figures out which tenant the current request belongs to. |
| `PolarSharp.MultiTenant` (same package, separate feature) | `services.AddPolarTenantLifecycle(config)` | `ITenantStatusService` so your code can suspend / reactivate / deactivate / delete tenants. Publishes `TenantStatusChangedNotification` to anyone subscribed. |
| `PolarSharp.MultiTenant.Notifications` | `services.AddPolarMultiTenantNotifications(config)` | Email + SMS + webhook dispatch whenever a tenant status changes. Subscribes to the lifecycle events automatically. |
| `PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite` | `builder.UseSqlite("/path/to/dir")` | SQLite-backed tenant registry. Stores everything platform-related in a single `master_SaaS.db` file at the path you provide. Per-tenant data goes in `{tenantId}.db` files in the same directory. |
| (same package, Litestream sub-feature) | implicit via `UseSqlite(...)` (defaults on); gate via `UseLitestream` in appsettings | The Litestream config generator + the auto-regenerator that updates the config whenever a tenant is suspended or reactivated. Inert when `UseLitestream` is `false`. |
| `PolarSharp.MultiTenant.EntityFrameworkCore.SqlServer` | `builder.UseSqlServer(connectionString)` | SQL Server-backed tenant registry instead of SQLite. Pick this if you already run SQL Server in your stack. |
| `PolarSharp.MultiTenant.EntityFrameworkCore.Postgres` | `builder.UsePostgres(connectionString)` | Postgres-backed tenant registry. |
| `PolarSharp.MultiTenant.EntityFrameworkCore.MariaDb` | `builder.UseMariaDb(connectionString)` | MariaDB / MySQL-backed tenant registry. |
| `PolarSharp.MultiTenant.EntityFrameworkCore.CosmosDb` | `builder.UseCosmosDb(...)` | Azure Cosmos DB-backed tenant registry. |
| `PolarSharp.MultiTenant.EntityFrameworkCore` (any provider) | `services.AddPolarSingleTenantUpgrade(config)` | The one-time helper that migrates an existing single-tenant deployment into a multi-tenant one at first MT startup. Safe to leave registered after the upgrade completes — it only runs once. |
| `PolarSharp.PrepaidWallets` | `services.AddPolarPrepaidWallets()` | The prepaid wallet aggregate + command handlers + projections + the MediatR registration for this package's assembly. |
| `PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.<provider>` | `services.Use<Provider>WalletEventStore(connectionString)` | The storage backend for the wallet's event store. Pick the provider that matches the rest of your stack. |
| `PolarSharp.EcommerceStoreManagement.EntityFrameworkCore` | `services.AddPolarEcommerce(config)` | The ecommerce catalog services. |
| `PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.<provider>` | `services.Use<Provider>Catalog(...)` | The storage backend for the ecommerce catalog. |
| `PolarSharp.Reporting.EntityFrameworkCore` | `services.AddPolarReporting(config)` | The reporting services. |
| `PolarSharp.Reporting.EntityFrameworkCore.<provider>` | `services.Use<Provider>Reporting(...)` | The storage backend for the reporting subsystem. |
| `PolarSharp.DataSeeding` | `services.AddPolarDataSeeding(config)` | The data-seeding helpers — useful for demos and integration tests. |
| `PolarSharp.EcommerceStorefronts` | `services.AddPolarStorefrontsCore(...)` | The storefront-rendering core. |
| `PolarSharp.EcommerceStorefronts.AspNetCore` | `services.AddPolarStorefronts(...)` | The ASP.NET Core middleware that serves storefronts. |
| `PolarSharp.EcommerceStorefronts.GuestSessions` | `services.AddPolarGuestSessions()` | Lets shoppers add items to a cart before they sign in. |
| `PolarSharp.CustomerGraph.Neo4j` | `services.AddPolarCustomerGraphNeo4j(...)` | The optional Neo4j-backed customer relationship graph. |

This table is not exhaustive — there are a couple of dozen packages in the family — but it covers everything you would reach for in the first three or four scenarios most teams encounter. If you install a package whose extension is not listed here, look in that package's README; the convention is rigorously consistent: one package, one `AddPolarSomething(...)` (or `UsePolarSomething(...)` / `UseSomething(...)`) extension, named to match the feature.

## Things to know

Plain-language gotchas, edge cases, and optional settings. Scan as needed.

- **The order of extension calls almost never matters.** DI is order-independent for almost everything PolarSharp does. There is essentially one exception: when you use the builder-style chain (`AddPolarInfrastructure(...).AddPolarMultiTenant().UseSqlite(...)`), the `.UseSqlite(...)` part needs to come *after* `.AddPolarMultiTenant()` because it adds onto the builder that `AddPolarMultiTenant()` returns. For the standalone `services.AddPolarXxx(...)` calls, you can list them in any order you like — DI resolves at request time, not at registration time.

- **MediatR is currently used by these PolarSharp packages.** The tenant lifecycle service (in `PolarSharp.MultiTenant`), the notification dispatcher (in `PolarSharp.MultiTenant.Notifications`), the Litestream auto-regenerator (in `PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite`, when Litestream is enabled), and the prepaid wallet (in `PolarSharp.PrepaidWallets`). As more packages start using MediatR in future releases, the same composition pattern keeps working — you will not need to change your startup file beyond adding the new extension call.

- **You do not need a separate `services.AddMediatR(...)` line for PolarSharp.** Each PolarSharp package that uses MediatR handles its own MediatR registration. If your *own* application code also uses MediatR (for example, you have your own command handlers in your own assembly), keep registering your own MediatR call too. They compose with each other — they do not replace each other. Your handlers and PolarSharp's handlers will live happily in the same MediatR instance.

- **You can mix and match storage providers.** SQL Server for the tenant registry, SQLite for the wallet's event store, Postgres for reporting — totally fine. Each provider extension manages its own storage independently. The packages do not assume they share a backend.

- **Litestream is opt-in in two layers, not one.** First layer: did you call `UseSqlite(...)` with `enableLitestream: true` (the default)? If yes, the Litestream-related services are registered. Second layer: did you set `PolarSharp:MultiTenant:Sqlite:Litestream:UseLitestream = true` in `appsettings.json`? If yes, those services actually do work. If no, they self-disable and do nothing. This two-layer design means you can leave `enableLitestream: true` in your startup file and toggle Litestream on for production but off for local development, just by changing one appsettings value per environment — no code change needed.

- **Single-tenant deployments still benefit from the lifecycle service.** Even if you only have one tenant ever, you can still suspend / deactivate / delete it programmatically, and the notifications still fire. The single-tenant case is just "N tenants where N happens to equal 1." There is no separate single-tenant code path you have to choose.

- **If you are not sure what to install for your specific scenario, start with Scenario 1.** Add packages and extension calls as you actually need them. You can always add more later without breaking what is already working. PolarSharp's extensions are designed to compose additively — adding a new one will not retroactively break the old ones.

- **Forgetting to install the package itself (as opposed to forgetting to call the extension) produces a much louder error.** If your startup file calls `AddPolarMultiTenantNotifications(...)` but you never ran `dotnet add package PolarSharp.MultiTenant.Notifications`, your application will not compile. So at least one of the two failure modes is loud. The quiet failure mode is the one to worry about — package installed, extension forgotten.

- **The `AddPolarInfrastructure(...)` call is the one truly required call.** Everything else is optional. There is no PolarSharp feature that works without infrastructure being registered first, because every other feature ultimately depends on the `PolarClient` it provides.
