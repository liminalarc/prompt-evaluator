using Api.EvalRuns;
using Api.Version;
using Application.EvalRuns;
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var postgres = builder.Configuration.GetConnectionString("Postgres") ?? "";
var evalRunnerBaseUrl = builder.Configuration["EvalRunner:BaseUrl"] ?? "http://localhost:8000";

builder.Services.AddInfrastructure(postgres, evalRunnerBaseUrl);
builder.Services.AddScoped<CreateEvalRunHandler>();

// Allow the Angular dev server to reach the API during per-process development.
const string DevCors = "dev-cors";
builder.Services.AddCors(options => options.AddPolicy(DevCors, policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors(DevCors);

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapVersionEndpoints();
app.MapEvalRunEndpoints();

// Apply migrations on startup once a database is configured (skipped when none is,
// e.g. the bare /health integration test).
if (!string.IsNullOrWhiteSpace(postgres))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<EvalDbContext>();
    db.Database.Migrate();
}

app.Run();

// Exposed so the test host (WebApplicationFactory<Program>) can boot the app.
public partial class Program;
