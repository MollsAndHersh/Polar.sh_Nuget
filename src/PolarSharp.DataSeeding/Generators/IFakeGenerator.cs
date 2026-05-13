using Bogus;

namespace PolarSharp.DataSeeding.Generators;

/// <summary>
/// Produces a populated domain record of type <typeparamref name="T"/> from a Bogus
/// <see cref="Faker"/>. Implementations are stateless — pass a seeded <see cref="Faker"/>
/// to get deterministic output (essential for CI reproducibility).
/// </summary>
/// <typeparam name="T">The domain record type produced.</typeparam>
public interface IFakeGenerator<T>
{
    /// <summary>Returns a populated <typeparamref name="T"/>. Sets <c>IsFakeData = true</c> on entities that support it.</summary>
    /// <param name="tenantId">The tenant the record belongs to.</param>
    /// <param name="faker">A Bogus <see cref="Faker"/> — supply one with a fixed seed for deterministic runs.</param>
    T Generate(string tenantId, Faker faker);
}

/// <summary>Convenience helpers for <see cref="IFakeGenerator{T}"/>.</summary>
public static class FakeGeneratorExtensions
{
    /// <summary>Produces <paramref name="count"/> records by repeatedly calling <see cref="IFakeGenerator{T}.Generate"/>.</summary>
    public static IEnumerable<T> GenerateMany<T>(this IFakeGenerator<T> generator, string tenantId, Faker faker, int count)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(faker);
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), "Count must be >= 0.");
        for (var i = 0; i < count; i++) yield return generator.Generate(tenantId, faker);
    }
}
