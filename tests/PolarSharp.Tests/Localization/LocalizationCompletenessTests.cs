using System.Globalization;
using System.Reflection;
using System.Resources;
using PolarSharp.Localization;

namespace PolarSharp.Tests.Localization;

public sealed class LocalizationCompletenessTests
{
    private static readonly ResourceManager ResourceManager = new(
        "PolarSharp.Localization.Resources.PolarMessages",
        typeof(PolarResourceLocalizer).Assembly);

    private static IEnumerable<string> AllKeys() =>
        typeof(PolarMessageKeys)
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
    public void AllMessageKeys_PresentIn_Resx(string culture)
    {
        var ci = CultureInfo.GetCultureInfo(culture);
        var missingKeys = AllKeys()
            .Where(key => ResourceManager.GetString(key, ci) is null)
            .ToList();

        Assert.Empty(missingKeys);
    }

    [Fact]
    public void AllMessageKeys_HaveNonEmptyEnglishValues()
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
