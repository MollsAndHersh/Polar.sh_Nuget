using PolarSharp;

namespace PolarTestApp.Endpoints;

internal static class MeterEndpoints
{
    internal static WebApplication MapMeterEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/test/meters").WithTags("Meters");

        group.MapGet("/", async (PolarClient polar, CancellationToken ct) =>
        {
            var result = await polar.Meters.EmptyPathSegment.GetAsync(cancellationToken: ct);
            return Results.Ok(result);
        })
        .WithName("ListMeters")
        .WithSummary("List meters from Polar sandbox.");

        group.MapGet("/{id}", async (string id, PolarClient polar, CancellationToken ct) =>
        {
            var result = await polar.Meters[id].GetAsync(cancellationToken: ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetMeter")
        .WithSummary("Get a single meter by ID.");

        return app;
    }
}
