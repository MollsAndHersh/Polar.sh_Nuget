namespace PolarSharp.EcommerceStoreManagement.Cloning;

/// <summary>Typed failure returned from a cloning operation.</summary>
public sealed record CloningError(CloningErrorKind Kind, string Message);

/// <summary>Discriminator for the kind of cloning failure.</summary>
public enum CloningErrorKind
{
    /// <summary>The source entity id is unknown to the current tenant's catalog.</summary>
    SourceNotFound,

    /// <summary>The caller supplied an override that conflicts with an existing unique key
    /// (e.g. a discount-code override that's already in use by another discount in the same
    /// tenant). The clone was rolled back.</summary>
    OverrideConflictsWithExistingRow,

    /// <summary>Auto-suffix collision avoidance ran out of attempts (more than 100 existing
    /// copies of the same name). Suggests the host should prompt the operator for a new
    /// name explicitly.</summary>
    NameCollisionExhausted,

    /// <summary>The local catalog repository write failed for an unrelated reason
    /// (constraint violation, connection failure). The clone was rolled back.</summary>
    PersistenceFailed,
}
