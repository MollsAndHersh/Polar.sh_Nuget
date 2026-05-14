using PolarSharp;
using PolarSharp.Generated.V1.CustomerSessions;

namespace PolarTestApp.Endpoints;

internal static class CustomerPortalEndpoints
{
    internal static WebApplication MapCustomerPortalEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/test/customer-portal").WithTags("CustomerPortal");

        // POST /test/customer-portal/session
        // Creates a customer session (Customer Access Token) using the OAT-authenticated PolarClient.
        // Requires a customer_id in the request body.
        group.MapPost("/session", async (
            CreateSessionRequest request,
            PolarClient polar,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.CustomerId))
                return Results.BadRequest(new { error = "customer_id is required." });

            var body = new EmptyPathSegmentRequestBuilder.PostRequestBody
            {
                CustomerSessionCustomerIDCreate = new()
                {
                    CustomerId = request.CustomerId,
                },
            };

            var session = await polar.CustomerSessions.EmptyPathSegment.PostAsync(body, cancellationToken: ct);

            if (session is null)
                return Results.Problem("Polar returned no session. Check that the customer_id exists in your sandbox.");

            return Results.Ok(new
            {
                sessionId = session.Id,
                customerId = session.CustomerId,
                customerPortalUrl = session.CustomerPortalUrl,
                token = MaskToken(session.Token),
                note = "The full token is masked here. In production, pass it directly to CreateCustomerPortalClient().",
            });
        })
        .WithName("CreateCustomerSession")
        .WithSummary("Create a Customer Portal session (Customer Access Token) for the given customer ID.")
        .WithDescription("""
            Creates a customer session using the OAT-authenticated PolarClient, then returns
            the session details. The token is masked in this response — in production it
            would be passed to polar.CreateCustomerPortalClient(token) to instantiate a
            portal client scoped to that customer.

            Demonstrates the OAT → Customer Access Token security handoff.
            """);

        // GET /test/customer-portal/orders
        // Demonstrates isolation: uses a portal client built from a hardcoded demo token.
        // In a real app the token comes from a session created by the endpoint above.
        group.MapGet("/orders", async (
            string? customerToken,
            PolarClient polar,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(customerToken))
            {
                return Results.BadRequest(new
                {
                    error = "Supply ?customerToken=<token> obtained from POST /test/customer-portal/session.",
                });
            }

            using var portalClient = polar.CreateCustomerPortalClient(customerToken);
            var result = await portalClient.Orders.EmptyPathSegment.GetAsync(cancellationToken: ct);
            return Results.Ok(result);
        })
        .WithName("GetCustomerPortalOrders")
        .WithSummary("List orders via the Customer Portal client (uses Customer Access Token, not OAT).")
        .WithDescription("""
            Supply ?customerToken=<token> obtained from POST /test/customer-portal/session.

            Demonstrates the security boundary: the portal client uses a Customer Access Token
            that cannot access org-level APIs — it's a completely separate HttpClient instance.
            """);

        group.MapGet("/subscriptions", async (
            string? customerToken,
            PolarClient polar,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(customerToken))
            {
                return Results.BadRequest(new
                {
                    error = "Supply ?customerToken=<token> obtained from POST /test/customer-portal/session.",
                });
            }

            using var portalClient = polar.CreateCustomerPortalClient(customerToken);
            var result = await portalClient.Subscriptions.EmptyPathSegment.GetAsync(cancellationToken: ct);
            return Results.Ok(result);
        })
        .WithName("GetCustomerPortalSubscriptions")
        .WithSummary("List subscriptions via the Customer Portal client (uses Customer Access Token, not OAT).");

        return app;
    }

    private static string MaskToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return "(null)";
        if (token.Length <= 8)
            return "***";
        return $"{token[..6]}...{token[^4..]}";
    }

}

internal sealed record CreateSessionRequest(string CustomerId);
