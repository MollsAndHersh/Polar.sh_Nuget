using System.Reflection;
using PolarSharp.Webhooks.Events;
using PolarSharp.Webhooks.Serialization;

namespace PolarSharp.Webhooks.Tests;

public sealed class KnownWebhookEventTypesTests
{
    // Enumerate all concrete (non-abstract) WebhookEvent subtypes in the assembly.
    private static IReadOnlyList<Type> ConcreteEventTypesInAssembly()
        => typeof(WebhookEvent).Assembly
            .GetTypes()
            .Where(t =>
                t.IsClass &&
                !t.IsAbstract &&
                t.Namespace == "PolarSharp.Webhooks.Events" &&
                typeof(WebhookEvent).IsAssignableFrom(t) &&
                // Exclude the catch-all placeholder — it is intentionally NOT in the static list
                t != typeof(UnknownWebhookEvent))
            .ToList();

    [Fact]
    public void All_ContainsNoDuplicates()
    {
        var duplicates = KnownWebhookEventTypes.All
            .GroupBy(t => t)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key.Name)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void All_CountMatchesConcreteEventTypesInEventsNamespace()
    {
        var discovered = ConcreteEventTypesInAssembly();
        Assert.Equal(discovered.Count, KnownWebhookEventTypes.All.Count);
    }

    [Fact]
    public void All_ContainsEveryConcreteEventTypeInEventsNamespace()
    {
        var discovered = ConcreteEventTypesInAssembly().ToHashSet();
        var missing = discovered.Except(KnownWebhookEventTypes.All).Select(t => t.Name).ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void All_ContainsNoTypesAbsentFromEventsNamespace()
    {
        var discovered = ConcreteEventTypesInAssembly().ToHashSet();
        var extra = KnownWebhookEventTypes.All.Except(discovered).Select(t => t.Name).ToList();

        Assert.Empty(extra);
    }

    [Fact]
    public void All_AllTypesInheritFromWebhookEvent()
    {
        var nonEvent = KnownWebhookEventTypes.All
            .Where(t => !typeof(WebhookEvent).IsAssignableFrom(t))
            .Select(t => t.Name)
            .ToList();

        Assert.Empty(nonEvent);
    }
}
