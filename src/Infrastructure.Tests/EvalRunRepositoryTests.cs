using Domain;
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Infrastructure.Tests;

public sealed class EvalRunRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16")
        .Build();

    private EvalDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var options = new DbContextOptionsBuilder<EvalDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        _db = new EvalDbContext(options);
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Add_then_GetById_round_trips_through_the_migrated_schema()
    {
        var repo = new EvalRunRepository(_db);
        var run = EvalRun.Create("ping", "ping", new DateTimeOffset(2026, 7, 11, 0, 0, 0, TimeSpan.Zero));

        await repo.AddAsync(run);
        var loaded = await repo.GetByIdAsync(run.Id);

        Assert.NotNull(loaded);
        Assert.Equal(run.Id, loaded!.Id);
        Assert.Equal("ping", loaded.Prompt);
        Assert.Equal("ping", loaded.Output);
    }

    [Fact]
    public async Task SystemInfo_reports_the_postgres_engine_version()
    {
        var systemInfo = new SystemInfo(_db);

        var version = await systemInfo.GetDatabaseVersionAsync();

        Assert.StartsWith("PostgreSQL", version);
    }
}
