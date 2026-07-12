namespace Application;

/// <summary>Version metadata a service reports about itself.</summary>
public sealed record ServiceVersion(string Service, string Version, string Commit);
