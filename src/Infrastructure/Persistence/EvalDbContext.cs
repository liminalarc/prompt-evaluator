using Microsoft.EntityFrameworkCore;
using Domain;

namespace Infrastructure.Persistence;

public sealed class EvalDbContext(DbContextOptions<EvalDbContext> options) : DbContext(options)
{
    public DbSet<EvalRun> EvalRuns => Set<EvalRun>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<Prompt> Prompts => Set<Prompt>();
    public DbSet<Dataset> Datasets => Set<Dataset>();
    public DbSet<ScorerConfig> ScorerConfigs => Set<ScorerConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Only the domain configurations — scoped by namespace so the Identity bounded context's
        // configurations (a separate DbContext, same assembly) don't leak into this model.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(EvalDbContext).Assembly,
            t => t.Namespace == "Infrastructure.Persistence.Configurations");
    }
}
