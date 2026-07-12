namespace Application.Ports;

/// <summary>Runtime facts about the backing infrastructure (e.g. the database engine version).</summary>
public interface ISystemInfo
{
    Task<string> GetDatabaseVersionAsync(CancellationToken ct = default);
}
