using Api.Analytics;
using Api.Datasets;
using Api.EvalRuns;
using Api.Folders;
using Api.Organizations;
using Api.Prompts;
using Api.Seam;
using Api.Version;
using Application.Analytics;
using Application.Datasets;
using Application.EvalRuns;
using Application.Folders;
using Application.Prompts;
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var postgres = builder.Configuration.GetConnectionString("Postgres") ?? "";
var evalRunnerBaseUrl = builder.Configuration["EvalRunner:BaseUrl"] ?? "http://localhost:8000";

builder.Services.AddInfrastructure(postgres, evalRunnerBaseUrl);
builder.Services.AddScoped<CreatePromptHandler>();
builder.Services.AddScoped<AddPromptVersionHandler>();
builder.Services.AddScoped<CreateFolderHandler>();
builder.Services.AddScoped<RenameFolderHandler>();
builder.Services.AddScoped<MoveFolderHandler>();
builder.Services.AddScoped<MovePromptHandler>();
builder.Services.AddScoped<CreateDatasetHandler>();
builder.Services.AddScoped<CaptureFixturesHandler>();
builder.Services.AddScoped<GenerateSyntheticFixturesHandler>();
builder.Services.AddScoped<ConfigureDatasetScorersHandler>();
builder.Services.AddScoped<RunEvaluationHandler>();
builder.Services.AddScoped<TrendAnalyticsHandler>();
builder.Services.AddScoped<RegressionAnalyticsHandler>();
builder.Services.AddScoped<ComparisonAnalyticsHandler>();
builder.Services.AddSingleton<RegressionDetector>();
builder.Services.AddSingleton<FixtureRedactor>();

// Allow the Angular dev server to reach the API during per-process development.
const string DevCors = "dev-cors";
builder.Services.AddCors(options => options.AddPolicy(DevCors, policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors(DevCors);

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapVersionEndpoints();
app.MapEchoEndpoints();
app.MapOrganizationEndpoints();
app.MapPromptEndpoints();
app.MapFolderEndpoints();
app.MapDatasetEndpoints();
app.MapEvalHarnessEndpoints();
app.MapAnalyticsEndpoints();

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
