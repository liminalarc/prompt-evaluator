using Domain;

namespace Api.Datasets;

public sealed record CreateDatasetRequest(string Name, string? Description);

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
    string PromptInput, string? Input, string? SlmOutput, string? DownstreamResult);

public sealed record CaptureFixturesRequest(List<CaptureTupleRequest> Tuples);

public sealed record FixtureResponse(
    Guid Id,
    string Origin,
    string Input,
    string? UpstreamContext,
    string? ExpectedOutput,
    Guid? SeedFixtureId,
    DateTimeOffset CreatedAt)
{
    public static FixtureResponse From(Fixture f) =>
        new(f.Id, f.Origin.ToString(), f.Input, f.UpstreamContext, f.ExpectedOutput, f.SeedFixtureId, f.CreatedAt);
}

public sealed record DatasetResponse(
    Guid Id,
    string Name,
    string? Description,
    IReadOnlyList<FixtureResponse> Fixtures)
{
    public static DatasetResponse From(Dataset d) =>
        new(d.Id, d.Name, d.Description, d.Fixtures.Select(FixtureResponse.From).ToList());
}

/// <summary>Lightweight projection for the browse/list view — fixture counts by origin, no bodies.</summary>
public sealed record DatasetSummaryResponse(
    Guid Id, string Name, string? Description, int FixtureCount, int CapturedCount, int SyntheticCount)
{
    public static DatasetSummaryResponse From(Dataset d) =>
        new(
            d.Id,
            d.Name,
            d.Description,
            d.Fixtures.Count,
            d.Fixtures.Count(f => f.Origin == FixtureOrigin.Captured),
            d.Fixtures.Count(f => f.Origin == FixtureOrigin.Synthetic));
}
