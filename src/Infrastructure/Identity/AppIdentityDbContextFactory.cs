using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Identity;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations --context AppIdentityDbContext</c> can build the
/// identity context without the Api composition root. The connection string here is used only for
/// scaffolding, never at runtime (mirrors <see cref="Persistence.EvalDbContextFactory"/>).
/// </summary>
public sealed class AppIdentityDbContextFactory : IDesignTimeDbContextFactory<AppIdentityDbContext>
{
    public AppIdentityDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppIdentityDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=litmusai;Username=postgres;Password=postgres",
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history_identity"))
            .Options;
        return new AppIdentityDbContext(options);
    }
}
