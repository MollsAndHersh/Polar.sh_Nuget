using System.Globalization;
using System.Reflection;
using System.Resources;
using PolarSharp.Webhooks.Localization;

namespace PolarSharp.Webhooks.Tests.Localization;

public sealed class WebhookLocalizationCompletenessTests
{
    private static readonly ResourceManager ResourceManager = new(
        "PolarSharp.Webhooks.Localization.Resources.PolarWebhookMessages",
        typeof(WebhookValidator).Assembly);

    private static IEnumerable<string> AllKeys() =>
        typeof(PolarWebhookMessageKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly)
            .Select(f => (string)f.GetRawConstantValue()!);

    public static IEnumerable<object[]> Cultures() =>
    [
        ["en-US"],
        ["es-MX"],
    ];

    [Theory]
    [MemberData(nameof(Cultures))]
    public void AllWebhookMessageKeys_PresentIn_Resx(string culture)
    {
        var ci = CultureInfo.GetCultureInfo(culture);
        var missingKeys = AllKeys()
            .Where(key => ResourceManager.GetString(key, ci) is null)
            .ToList();

        Assert.Empty(missingKeys);
    }

    [Fact]
    public void AllWebhookMessageKeys_HaveNonEmptyEnglishValues()
    {
        var ci = CultureInfo.GetCultureInfo("en-US");
        var emptyKeys = AllKeys()
            .Where(key =>
            {
                var value = ResourceManager.GetString(key, ci);
                return string.IsNullOrWhiteSpace(value);
            })
            .ToList();

        Assert.Empty(emptyKeys);
    }
}
