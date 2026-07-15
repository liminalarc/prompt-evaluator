using Application.Ports;
using Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Persistence;

public sealed class FolderRepository(EvalDbContext db) : IFolderRepository
{
    public async Task AddAsync(Folder folder, CancellationToken ct = default)
    {
        await db.Folders.AddAsync(folder, ct);
        await db.SaveChangesAsync(ct);
    }

    public Task<Folder?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Folders.SingleOrDefaultAsync(f => f.Id == id, ct);

    public async Task<IReadOnlyList<Folder>> ListAsync(CancellationToken ct = default)
        => await db.Folders.ToListAsync(ct);

    public async Task<IReadOnlyList<Folder>> ListByOrganizationAsync(Guid organizationId, CancellationToken ct = default)
        => await db.Folders.Where(f => f.OrganizationId == organizationId).ToListAsync(ct);

    // Walk parents to the root in a single query. Folder trees are tiny, so the recursive CTE is
    // cheap and keeps folder-move a single-row update (no denormalized root ref to cascade).
    public async Task<Guid?> GetTopLevelAncestorIdAsync(Guid folderId, CancellationToken ct = default)
    {
        const string sql = """
            WITH RECURSIVE ancestors AS (
                SELECT "Id", "ParentId" FROM folders WHERE "Id" = @id
                UNION ALL
                SELECT f."Id", f."ParentId"
                FROM folders f
                JOIN ancestors a ON f."Id" = a."ParentId"
            )
            SELECT "Id" FROM ancestors WHERE "ParentId" IS NULL
            """;

        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;
        if (opened)
            await connection.OpenAsync(ct);
        try
        {
            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("id", folderId);
            var result = await cmd.ExecuteScalarAsync(ct);
            return result is Guid id ? id : null;
        }
        finally
        {
            if (opened)
                await connection.CloseAsync();
        }
    }

    public async Task<IReadOnlyList<Guid>> GetDescendantIdsAsync(Guid folderId, CancellationToken ct = default)
    {
        const string sql = """
            WITH RECURSIVE descendants AS (
                SELECT "Id" FROM folders WHERE "ParentId" = @id
                UNION ALL
                SELECT f."Id"
                FROM folders f
                JOIN descendants d ON f."ParentId" = d."Id"
            )
            SELECT "Id" FROM descendants
            """;

        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;
        if (opened)
            await connection.OpenAsync(ct);
        try
        {
            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("id", folderId);
            var ids = new List<Guid>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                ids.Add(reader.GetGuid(0));
            return ids;
        }
        finally
        {
            if (opened)
                await connection.CloseAsync();
        }
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var folder = await db.Folders
            .AsNoTracking()
            .SingleOrDefaultAsync(f => f.Id == id, ct);
        if (folder is null)
            return;

        // Reparent to this folder's parent — null when it is top-level, which promotes children to
        // top-level and unfiles prompts to the org root (1.10, least-destructive). One transaction so
        // the folder is never deleted while something still points at it.
        var newParentId = folder.ParentId;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        await db.Folders
            .Where(f => f.ParentId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.ParentId, newParentId), ct);
        await db.Prompts
            .Where(p => p.FolderId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.FolderId, newParentId), ct);
        await db.Folders.Where(f => f.Id == id).ExecuteDeleteAsync(ct);

        await tx.CommitAsync(ct);
    }
}
