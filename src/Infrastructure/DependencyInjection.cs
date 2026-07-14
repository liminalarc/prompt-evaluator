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
        string evalRunnerBaseUrl)
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
        services.AddScoped<ISystemInfo, SystemInfo>();
        services.AddScoped<Application.Scoring.ScorerFactory>();
        services.AddSingleton(TimeProvider.System);

        services.AddHttpClient<IEvaluationRunner, EvalRunnerClient>(client =>
            client.BaseAddress = new Uri(evalRunnerBaseUrl));

        return services;
    }
}
