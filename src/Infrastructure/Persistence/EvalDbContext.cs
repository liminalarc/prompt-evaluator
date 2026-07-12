using Microsoft.EntityFrameworkCore;
using Domain;

namespace Infrastructure.Persistence;

public sealed class EvalDbContext(DbContextOptions<EvalDbContext> options) : DbContext(options)
{
    public DbSet<EvalRun> EvalRuns => Set<EvalRun>();
    public DbSet<Prompt> Prompts => Set<Prompt>();
    public DbSet<Dataset> Datasets => Set<Dataset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EvalDbContext).Assembly);
    }
}
