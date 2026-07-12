namespace Domain;

/// <summary>
/// Where a <see cref="Fixture"/> came from. Capture-first: <c>Captured</c> fixtures are sampled
/// from the apps (ground truth); <c>Synthetic</c> fixtures are generated to fill coverage gaps,
/// seeded from captured examples. Origin is always recorded; the harness treats a fixture the
/// same regardless of origin.
/// </summary>
public enum FixtureOrigin
{
    Captured,
    Synthetic,
}
