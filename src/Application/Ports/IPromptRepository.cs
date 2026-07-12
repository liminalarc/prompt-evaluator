using Domain;

namespace Application.Ports;

/// <summary>
/// Persistence port for the <see cref="Prompt"/> aggregate and its full version history.
///
/// <para>
/// This is the deliberate <b>Zatomic seam</b>. Prompts are <i>copied into</i> our own
/// registry today, persisted by the EF Core (Postgres) adapter in Infrastructure. The port
/// exists so that Zatomic — our own prompt/version tool — can become the backing store later
/// by supplying a different adapter, with <b>no change to Domain or Application</b>. No domain
/// type depends on the storage choice.
/// </para>
/// </summary>
public interface IPromptRepository
{
    /// <summary>Persists a newly created prompt, including any versions already added to it.</summary>
    Task AddAsync(Prompt prompt, CancellationToken ct = default);

    /// <summary>Loads a prompt with its full, ordered version history, or null if none exists.</summary>
    Task<Prompt?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>All prompts with their version history, for browsing.</summary>
    Task<IReadOnlyList<Prompt>> ListAsync(CancellationToken ct = default);

    /// <summary>Persists changes made to a tracked aggregate (e.g. a newly appended version).</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
