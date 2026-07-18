using Domain;

namespace Api.Datasets;

/// <summary>Body for creating a dataset under a prompt — the owning prompt comes from the route.</summary>
public sealed record CreateDatasetUnderPromptRequest(string Name, string? Description);

/// <summary>
/// One captured tuple in the documented capture-ingestion schema, in provenance order:
/// upstream <see cref="Input"/> to the SLM → the SLM's <see cref="SlmOutput"/> → the prompt's
/// actual <see cref="PromptInput"/> → optional <see cref="DownstreamResult"/>.
///
/// <para>
/// Mapping onto a <see cref="Fixture"/>: <see cref="PromptInput"/> (required) → the fixture's
/// input; <see cref="SlmOutput"/> → its upstream context (the immediate provenance the eval
/// needs); <see cref="DownstreamResult"/> → its expected/reference output. <see cref="Input"/>
/// (the SLM's own upstream input, one hop further back) is accepted for schema completeness but
/// not persisted as a separate fixture field — the fixture keeps the spec's three text fields.
/// </para>
/// </summary>
public sealed record CaptureTupleRequest(
    string PromptInput, string? Input, string? SlmOutput, string? DownstreamResult,
    string? Origin = null, string? Label = null, string? Description = null);

public sealed record CaptureFixturesRequest(List<CaptureTupleRequest> Tuples);

/// <summary>Editable fixture metadata — label + description; input/origin/seed are fixed (U7).</summary>
public sealed record EditFixtureRequest(string? Label, string? Description);

public sealed record GenerationGuidanceRequest(string? CoverageGoals, string? EdgeCases, string? Constraints);

public sealed record GenerateFixturesRequest(GenerationGuidanceRequest? Guidance, int? Count);

public sealed record FixtureResponse(
    Guid Id,
    string Origin,
    string? Label,
    string? Description,
    string Input,
    string? UpstreamContext,
    string? ExpectedOutput,
    Guid? SeedFixtureId,
    DateTimeOffset CreatedAt)
{
    public static FixtureResponse From(Fixture f) =>
        new(f.Id, f.Origin.ToString(), f.Label, f.Description, f.Input, f.UpstreamContext, f.ExpectedOutput, f.SeedFixtureId, f.CreatedAt);
}

public sealed record DatasetResponse(
    Guid Id,
    Guid PromptId,
    string Name,
    string? Description,
    IReadOnlyList<FixtureResponse> Fixtures)
{
    public static DatasetResponse From(Dataset d) =>
        new(d.Id, d.PromptId, d.Name, d.Description, d.Fixtures.Select(FixtureResponse.From).ToList());
}

/// <summary>Lightweight projection for the browse/list view — fixture counts by origin, no bodies.</summary>
public sealed record DatasetSummaryResponse(
    Guid Id, Guid PromptId, string Name, string? Description, int FixtureCount, int CapturedCount, int SyntheticCount)
{
    public static DatasetSummaryResponse From(Dataset d) =>
        new(
            d.Id,
            d.PromptId,
            d.Name,
            d.Description,
            d.Fixtures.Count,
            d.Fixtures.Count(f => f.Origin == FixtureOrigin.Captured),
            d.Fixtures.Count(f => f.Origin == FixtureOrigin.Synthetic));
}
