using PolarSharp;

namespace PolarTestApp.Endpoints;

internal static class RefundEndpoints
{
    internal static WebApplication MapRefundEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/test/refunds").WithTags("Refunds");

        group.MapGet("/", async (PolarClient polar, CancellationToken ct) =>
        {
            var result = await polar.Refunds.EmptyPathSegment.GetAsync(cancellationToken: ct);
            return Results.Ok(result);
        })
        .WithName("ListRefunds")
        .WithSummary("List refunds from Polar sandbox.");

        return app;
    }
}
