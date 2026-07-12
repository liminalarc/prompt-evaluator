using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Persistence;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations</c> can build the context without the Api
/// composition root. The connection string here is used only for scaffolding, never at runtime.
/// </summary>
public sealed class EvalDbContextFactory : IDesignTimeDbContextFactory<EvalDbContext>
{
    public EvalDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<EvalDbContext>()
            .UseNpgsql("Host=localhost;Database=prompteval;Username=postgres;Password=postgres")
            .Options;
        return new EvalDbContext(options);
    }
}
