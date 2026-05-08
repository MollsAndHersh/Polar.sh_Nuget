using PolarSharp;

namespace PolarTestApp.Endpoints;

internal static class BenefitEndpoints
{
    internal static WebApplication MapBenefitEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/test/benefits").WithTags("Benefits");

        group.MapGet("/", async (PolarClient polar, CancellationToken ct) =>
        {
            var result = await polar.Benefits.EmptyPathSegment.GetAsync(cancellationToken: ct);
            return Results.Ok(result);
        })
        .WithName("ListBenefits")
        .WithSummary("List benefits from Polar sandbox.");

        group.MapGet("/grants", async (PolarClient polar, CancellationToken ct) =>
        {
            var result = await polar.BenefitGrants.EmptyPathSegment.GetAsync(cancellationToken: ct);
            return Results.Ok(result);
        })
        .WithName("ListBenefitGrants")
        .WithSummary("List benefit grants from Polar sandbox.");

        return app;
    }
}
