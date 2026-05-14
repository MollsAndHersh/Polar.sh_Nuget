using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PolarSharp.Extensions;
using PolarSharp.MultiTenant.Extensions;
using PolarSharp.MultiTenant.Identity;
using PolarSharp.Reporting.EntityFrameworkCore;
using PolarSharp.Reporting.EntityFrameworkCore.Snapshot;
using PolarSharp.Reporting.EntityFrameworkCore.Sqlite;
using PolarSharp.Reporting.Identity.Extensions;
using PolarSnapshotTestApp.Endpoints;
using PolarSnapshotTestApp.Seed;
using Scalar.AspNetCore;

// V20-005 Phase 3 end-to-end demo app: drives the per-tenant snapshot orchestrator
// via Identity sign-in/sign-out + per-request heartbeat middleware. SQLite-backed
// stores live in ./data/. Seeds a default test user (test@example.com / TestPass123)
// on first run.
//
// This app pulls EF Core into the dependency tree, which is known-not-fully-AOT-safe.
// For the AOT-clean library smoke test, see testapp/PolarTestApp/.

var builder = WebApplication.CreateBuilder(args);

var sandboxToken = Environment.GetEnvironmentVariable("POLAR_SANDBOX_TOKEN");
if (!string.IsNullOrWhiteSpace(sandboxToken))
{
    builder.Configuration["PolarSharp:AccessToken"] = sandboxToken;
}

builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant();

// ── Identity (SQLite) ───────────────────────────────────────────────────────
Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "data"));
var identityDbPath = Path.Combine(builder.Environment.ContentRootPath, "data", "polar-identity.db");
var reportingDbPath = Path.Combine(builder.Environment.ContentRootPath, "data", "polar-reporting.db");

builder.Services.AddDbContext<PolarUserDbContext>(opts =>
    opts.UseSqlite($"Data Source={identityDbPath}",
        sql => sql.MigrationsAssembly("PolarSharp.MultiTenant.Identity.Sqlite")));

// Standard Identity registration (NOT AddPolarIdentity, which is API-only — uses
// AddIdentityCore and skips SignInManager). The bridge needs SignInManager, so we
// use AddIdentity here for the full cookie + SignInManager pipeline.
builder.Services
    .AddIdentity<PolarApplicationUser, PolarApplicationRole>(opts =>
    {
        opts.User.RequireUniqueEmail = true;
        opts.Password.RequiredLength = 8;
        opts.Password.RequireDigit = true;
        opts.Password.RequireLowercase = true;
        opts.Password.RequireUppercase = false;
        opts.Password.RequireNonAlphanumeric = false;
    })
    .AddEntityFrameworkStores<PolarUserDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthorization();

// ── Reporting snapshot (SQLite) ─────────────────────────────────────────────
builder.Services.AddPolarReportingSnapshot(builder.Configuration);
builder.Services.UseSqliteReporting($"Data Source={reportingDbPath}");

// ── V20-005 Phase 3 bridge ──────────────────────────────────────────────────
builder.Services.AddPolarReportingIdentityHook();

// ── Test-app bring-up ───────────────────────────────────────────────────────
builder.Services.AddHostedService<TestAppDbInitializer>();
builder.Services.AddHostedService<TestUserSeeder>();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UsePolarInfrastructure();

// Order matters: Authentication populates HttpContext.User; the heartbeat
// middleware reads it; Authorization runs after.
app.UseAuthentication();
app.UsePolarReportingIdentityHeartbeat();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference();

app.MapIdentityEndpoints()
   .MapSnapshotEndpoints();

app.Run();
