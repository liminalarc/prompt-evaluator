using Application.Ports;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

public sealed class SystemInfo(EvalDbContext db) : ISystemInfo
{
    public async Task<string> GetDatabaseVersionAsync(CancellationToken ct = default)
    {
        // EF scalar SqlQuery maps a column named "Value".
        var rows = await db.Database
            .SqlQueryRaw<string>("SELECT version() AS \"Value\"")
            .ToListAsync(ct);
        return rows.FirstOrDefault() ?? "unknown";
    }
}
