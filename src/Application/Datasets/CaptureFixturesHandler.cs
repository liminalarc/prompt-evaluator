using Application.Ports;
using Domain;

namespace Application.Datasets;

/// <summary>
/// One captured tuple emitted by an app, in the documented capture-ingestion order
/// (upstream input → SLM output → the prompt's actual input → optional downstream result).
/// <see cref="PromptInput"/> is the prompt's actual input and is required; <see cref="SlmOutput"/>
/// is the immediate upstream context the input was derived from; <see cref="DownstreamResult"/>
/// is an optional reference output.
/// </summary>
public sealed record CapturedTuple(
    string PromptInput,
    string? SlmOutput,
    string? DownstreamResult,
    FixtureOrigin Origin = FixtureOrigin.Captured,
    string? Label = null,
    string? Description = null);

/// <summary>
/// Lands captured tuples as <see cref="FixtureOrigin.Captured"/> fixtures. Every text field is
/// run through <see cref="FixtureRedactor"/> before it is persisted — redaction is the ingest
/// hook. Returns null when the dataset does not exist so the Api can translate that to a 404.
/// </summary>
public sealed class CaptureFixturesHandler(
    IDatasetRepository repository,
    FixtureRedactor redactor,
    TimeProvider time)
{
    public async Task<Dataset?> HandleAsync(
        Guid datasetId,
        IReadOnlyList<CapturedTuple> tuples,
        CancellationToken ct = default)
    {
        var dataset = await repository.GetByIdAsync(datasetId, ct);
        if (dataset is null)
            return null;

        var now = time.GetUtcNow();
        foreach (var tuple in tuples)
        {
            // Manual-entry path (U8): the operator chooses the origin. Content is redacted at ingest;
            // the label/description are operator-authored metadata, so they pass through as-is.
            dataset.AddManualFixture(
                tuple.Origin,
                redactor.Redact(tuple.PromptInput)!,
                now,
                label: tuple.Label,
                description: tuple.Description,
                upstreamContext: redactor.Redact(tuple.SlmOutput),
                expectedOutput: redactor.Redact(tuple.DownstreamResult));
        }

        await repository.SaveChangesAsync(ct);
        return dataset;
    }
}
