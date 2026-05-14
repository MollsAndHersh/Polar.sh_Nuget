using Microsoft.AspNetCore.Identity;
using PolarSharp.MultiTenant.Identity;

namespace PolarSnapshotTestApp.Endpoints;

/// <summary>
/// V20-005 Phase 3 test-app Identity endpoints. Lets a developer drive the
/// SignInManager wrapper end-to-end (the wrapper auto-fires the snapshot
/// orchestrator). The seeded user is <c>test@example.com</c> / <c>TestPass123</c>.
/// </summary>
public static class IdentityEndpoints
{
    /// <summary>Maps the test-only /test/identity/* endpoints.</summary>
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/test/identity").WithTags("Identity");

        group.MapPost("/login", async (
            LoginRequest req,
            SignInManager<PolarApplicationUser> signIn,
            UserManager<PolarApplicationUser> users) =>
        {
            var user = await users.FindByEmailAsync(req.Email);
            if (user is null) return Results.NotFound(new { error = "User not found.", req.Email });

            var result = await signIn.PasswordSignInAsync(user, req.Password, isPersistent: false, lockoutOnFailure: false);
            if (!result.Succeeded) return Results.Unauthorized();

            return Results.Ok(new
            {
                signedInAs = user.Email,
                note = "PolarSnapshotSignInManager fired TriggerImmediateAsync + StartPeriodicAsync for the current tenant. Inspect with GET /test/snapshot/last.",
            });
        })
        .WithName("Login")
        .WithSummary("Sign in via PolarSnapshotSignInManager (drives the snapshot orchestrator).");

        group.MapPost("/logout", async (SignInManager<PolarApplicationUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.Ok(new
            {
                note = "PolarSnapshotSignInManager fired StopPeriodicAsync for the current tenant.",
            });
        })
        .WithName("Logout")
        .WithSummary("Sign out (drives StopPeriodicAsync on the snapshot orchestrator).");

        group.MapGet("/me", (HttpContext http) =>
        {
            if (http.User.Identity?.IsAuthenticated != true)
                return Results.Ok(new { authenticated = false });

            return Results.Ok(new
            {
                authenticated = true,
                name = http.User.Identity.Name,
                claims = http.User.Claims.Select(c => new { c.Type, c.Value }),
            });
        })
        .WithName("Me")
        .WithSummary("Show the current authenticated user (or { authenticated: false }).");

        return app;
    }
}

internal sealed record LoginRequest(string Email, string Password);
