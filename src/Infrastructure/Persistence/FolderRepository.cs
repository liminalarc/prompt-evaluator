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

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
