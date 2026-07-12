using Application.Ports;
using Infrastructure.Http;
using Infrastructure.Persistence;
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

        services.AddScoped<IEvalRunRepository, EvalRunRepository>();
        services.AddSingleton(TimeProvider.System);

        services.AddHttpClient<IEvaluationRunner, EvalRunnerClient>(client =>
            client.BaseAddress = new Uri(evalRunnerBaseUrl));

        return services;
    }
}
