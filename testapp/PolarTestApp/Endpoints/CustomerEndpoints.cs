using PolarSharp;
using PolarSharp.Generated.Models;

namespace PolarTestApp.Endpoints;

internal static class CustomerEndpoints
{
    internal static WebApplication MapCustomerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/test/customers").WithTags("Customers");

        group.MapGet("/", async (PolarClient polar, CancellationToken ct) =>
        {
            var result = await polar.Customers.EmptyPathSegment.GetAsync(cancellationToken: ct);
            return Results.Ok(result);
        })
        .WithName("ListCustomers")
        .WithSummary("List customers from Polar sandbox.");

        group.MapGet("/{id}", async (string id, PolarClient polar, CancellationToken ct) =>
        {
            var result = await polar.Customers[id].GetAsync(cancellationToken: ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetCustomer")
        .WithSummary("Get a single customer by ID.");

        group.MapPost("/", async (CreateCustomerRequest request, PolarClient polar, CancellationToken ct) =>
        {
            var body = new CustomerCreate
            {
                CustomerIndividualCreate = new CustomerIndividualCreate { Email = request.Email }
            };
            var result = await polar.Customers.EmptyPathSegment.PostAsync(body, cancellationToken: ct);
            return Results.Created($"/test/customers/{result?.Id}", result);
        })
        .WithName("CreateCustomer")
        .WithSummary("Create a new customer in Polar sandbox.");

        return app;
    }

    internal record CreateCustomerRequest(string Email);
}
