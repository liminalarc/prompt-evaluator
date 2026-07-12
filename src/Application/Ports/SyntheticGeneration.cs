namespace Application.Ports;

/// <summary>A captured example passed to the generator to anchor the distribution.</summary>
public sealed record SeedExampleData(string Input, string? UpstreamContext, string? ExpectedOutput);

/// <summary>
/// Operator guidance that steers *what* gets generated on top of the seed distribution —
/// coverage goals, edge-case / adversarial targets, and constraints.
/// </summary>
public sealed record GenerationGuidanceData(string? CoverageGoals, string? EdgeCases, string? Constraints);

/// <summary>
/// A fixture the generator produced. <see cref="SeedIndex"/> is the 0-based index into the
/// seed list it was derived from (null if the generator did not attribute one).
/// </summary>
public sealed record GeneratedFixtureData(
    string Input, string? UpstreamContext, string? ExpectedOutput, int? SeedIndex);
