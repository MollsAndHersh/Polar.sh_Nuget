using PolarSharp;

namespace PolarTestApp.Endpoints;

internal static class LicenseKeyEndpoints
{
    internal static WebApplication MapLicenseKeyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/test/license-keys").WithTags("LicenseKeys");

        group.MapGet("/", async (PolarClient polar, CancellationToken ct) =>
        {
            var result = await polar.LicenseKeys.EmptyPathSegment.GetAsync(cancellationToken: ct);
            return Results.Ok(result);
        })
        .WithName("ListLicenseKeys")
        .WithSummary("List license keys from Polar sandbox.");

        group.MapGet("/{id}", async (string id, PolarClient polar, CancellationToken ct) =>
        {
            var result = await polar.LicenseKeys[id].GetAsync(cancellationToken: ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetLicenseKey")
        .WithSummary("Get a single license key by ID.");

        return app;
    }
}
