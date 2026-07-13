using Microsoft.EntityFrameworkCore;
using Domain;

namespace Infrastructure.Persistence;

public sealed class EvalDbContext(DbContextOptions<EvalDbContext> options) : DbContext(options)
{
    public DbSet<EvalRun> EvalRuns => Set<EvalRun>();
    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<Prompt> Prompts => Set<Prompt>();
    public DbSet<Dataset> Datasets => Set<Dataset>();
    public DbSet<ScorerConfig> ScorerConfigs => Set<ScorerConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EvalDbContext).Assembly);
    }
}
