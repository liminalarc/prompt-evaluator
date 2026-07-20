using Application.Ports;
using Infrastructure.Email;
using Infrastructure.Http;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string postgresConnectionString,
        string evalRunnerBaseUrl,
        string? evalRunnerServiceToken = null)
    {
        services.AddDbContext<EvalDbContext>(options =>
            options.UseNpgsql(postgresConnectionString));

        // Identity bounded context (4.1): a separate store on the same Postgres. UserManager owns
        // credential hashing; the organization is the permission boundary (grants live here too).
        services.AddDbContext<AppIdentityDbContext>(options =>
            options.UseNpgsql(postgresConnectionString, npgsql =>
                // Separate migration history so the two contexts on one database don't collide.
                npgsql.MigrationsHistoryTable("__ef_migrations_history_identity")));
        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddEntityFrameworkStores<AppIdentityDbContext>();
        services.AddScoped<IUserDirectory, UserDirectory>();

        // Email seam (4.1): dev/CI default only logs; a real provider is wired at deploy (3.2).
        services.AddScoped<IEmailSender, LoggingEmailSender>();

        services.AddScoped<IEvalRunRepository, EvalRunRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IFolderRepository, FolderRepository>();
        services.AddScoped<IPromptRepository, PromptRepository>();
        services.AddScoped<IDatasetRepository, DatasetRepository>();
        services.AddScoped<IScorerConfigRepository, ScorerConfigRepository>();
        services.AddScoped<IModelCatalogRepository, ModelCatalogRepository>();
        services.AddScoped<ISystemInfo, SystemInfo>();
        services.AddScoped<Application.Scoring.ScorerFactory>();
        services.AddSingleton(TimeProvider.System);

        // AI-usage ledger (6.1): the ambient attribution carrier (pure AsyncLocal holder) and the
        // recorder. The recorder writes on its own unit of work (own DbContext per record) so a ledger
        // row survives even when the surrounding eval operation later fails.
        services.AddSingleton<Application.Ports.IAiUsageContextAccessor, Application.AiUsage.AmbientAiUsageContext>();
        services.AddSingleton<Application.Ports.IAiUsageRecorder, AiUsageRecorder>();

        // eval-runner is an internal trusted service (4.1): authenticate to it with a shared
        // service token attached by a DelegatingHandler. When the token is null/empty the handler
        // is a no-op, so dev/CI/test defaults keep working against an open eval-runner.
        services.AddHttpClient<IEvaluationRunner, EvalRunnerClient>(client =>
            {
                client.BaseAddress = new Uri(evalRunnerBaseUrl);
                // R1 interim band-aid: a heavy synchronous run (round-debrief — Sonnet generation +
                // Opus judge per fixture) sits right at the .NET default 100s HttpClient timeout and
                // 502s at the boundary. Give it generous headroom until async runs (R1) land. Note:
                // App Runner's own request router may still cap a truly long run — the real fix is R1.
                client.Timeout = TimeSpan.FromMinutes(5);
            })
            .AddHttpMessageHandler(() => new ServiceTokenHandler(evalRunnerServiceToken));

        return services;
    }
}
