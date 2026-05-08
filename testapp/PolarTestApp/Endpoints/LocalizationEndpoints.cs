using System.Globalization;
using PolarSharp;
using PolarSharp.Localization;

namespace PolarTestApp.Endpoints;

internal static class LocalizationEndpoints
{
    internal static WebApplication MapLocalizationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/test/localization").WithTags("Localization");

        // GET /test/localization/errors?culture=es-MX
        // Switches to the requested culture, then returns localized PolarSharp error message strings.
        // Proves IStringLocalizer resolves the correct .resx values per culture.
        group.MapGet("/errors", (
            string? culture,
            IPolarLocalizer localizer) =>
        {
            if (!string.IsNullOrWhiteSpace(culture))
            {
                try
                {
                    var cultureInfo = CultureInfo.GetCultureInfo(culture);
                    CultureInfo.CurrentCulture = cultureInfo;
                    CultureInfo.CurrentUICulture = cultureInfo;
                }
                catch (CultureNotFoundException)
                {
                    return Results.BadRequest(new { error = $"Unknown culture: '{culture}'." });
                }
            }

            return Results.Ok(new
            {
                requestedCulture = culture ?? "default (en-US)",
                appliedCulture = CultureInfo.CurrentUICulture.Name,
                localizedMessages = new
                {
                    Error_Unauthorized = localizer["Error_Unauthorized"].Value,
                    Error_Forbidden = localizer["Error_Forbidden"].Value,
                    Error_NotFound = localizer["Error_NotFound"].Value,
                    Error_RateLimit = localizer["Error_RateLimit", "30"].Value,
                    Error_Validation = localizer["Error_Validation", "field: required"].Value,
                    Error_ServerError = localizer["Error_ServerError"].Value,
                    Error_Timeout = localizer["Error_Timeout"].Value,
                    Error_NetworkFailure = localizer["Error_NetworkFailure"].Value,
                },
            });
        })
        .WithName("GetLocalizedErrors")
        .WithSummary("Returns PolarSharp error messages in the requested culture.")
        .WithDescription("""
            Pass ?culture=es-MX to get Spanish translations, or ?culture=en-US (default).

            Demonstrates that IStringLocalizer resolves the correct .resx values per culture.
            Built-in cultures: en-US, es-MX.
            """);

        group.MapGet("/cultures", () =>
        {
            return Results.Ok(new
            {
                builtInCultures = new[] { "en-US", "es-MX" },
                currentCulture = CultureInfo.CurrentCulture.Name,
                currentUICulture = CultureInfo.CurrentUICulture.Name,
                note = "Supply ?culture=<code> on /test/localization/errors to switch culture for that request.",
            });
        })
        .WithName("GetSupportedCultures")
        .WithSummary("Returns the cultures built into PolarSharp and the current request culture.");

        return app;
    }
}
