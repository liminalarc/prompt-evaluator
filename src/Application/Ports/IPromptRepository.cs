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

    /// <summary>
    /// The prompts filed directly in a folder (1.7). A null <paramref name="folderId"/> returns the
    /// unfiled prompts — the contents of the root folder.
    /// </summary>
    Task<IReadOnlyList<Prompt>> ListByFolderAsync(Guid? folderId, CancellationToken ct = default);

    /// <summary>
    /// The prompts belonging to an organization (1.9). The default filters <see cref="ListAsync"/>;
    /// the EF adapter overrides it with a DB-side query.
    /// </summary>
    async Task<IReadOnlyList<Prompt>> ListByOrganizationAsync(Guid organizationId, CancellationToken ct = default)
        => (await ListAsync(ct)).Where(p => p.OrganizationId == organizationId).ToList();

    /// <summary>Persists changes made to a tracked aggregate (e.g. a newly appended version).</summary>
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Deletes a prompt and everything owned by it (1.10): its version history, its datasets, and
    /// those datasets' fixtures, scorer configs, eval runs, and scores. No-op if it does not exist.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
