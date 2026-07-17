namespace Domain;

/// <summary>
/// A role a catalog model can serve in the eval loop. A model may serve several. Stored on the
/// aggregate as boolean flags (there is no <c>List&lt;enum&gt;</c> precedent in the schema and the
/// set is small and fixed), and surfaced as this enum via <see cref="ModelCatalogEntry.Roles"/>.
/// </summary>
public enum ModelRole
{
    /// <summary>Runs the prompt under test (the target/subject model).</summary>
    Subject,

    /// <summary>Scores output as the LLM judge.</summary>
    Judge,

    /// <summary>Generates synthetic fixtures.</summary>
    Generator,
}
