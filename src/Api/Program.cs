using Api.Analytics;
using Api.Auth;
using Api.Datasets;
using Api.EvalRuns;
using Api.Folders;
using Api.Organizations;
using Api.Prompts;
using Api.Seam;
using Api.Version;
using Application.Ports;
using Application.Analytics;
using Application.Datasets;
using Application.EvalRuns;
using Application.Folders;
using Application.Prompts;
using Infrastructure;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var postgres = builder.Configuration.GetConnectionString("Postgres") ?? "";
var evalRunnerBaseUrl = builder.Configuration["EvalRunner:BaseUrl"] ?? "http://localhost:8000";

builder.Services.AddInfrastructure(postgres, evalRunnerBaseUrl);

// Identity token providers (for password-reset tokens) live in the ASP.NET Core shared framework,
// so they're added here at the composition root rather than in the (framework-light) Infrastructure.
new IdentityBuilder(typeof(AppUser), builder.Services).AddDefaultTokenProviders();
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

// Authentication (4.1): a cookie session over the Identity bounded context. The redirect events
// are overridden to answer 401/403 so the cookie stack behaves like an API, not an HTML site.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddScoped<OrgAccess>();
builder.Services
    .AddAuthentication(AuthEndpoints.Scheme)
    .AddCookie(AuthEndpoints.Scheme, options =>
    {
        options.Cookie.Name = "litmus.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    });
builder.Services.AddAuthorization();

// Allow the Angular dev server to reach the API during per-process development. Credentials are
// enabled for the auth cookie, so the origin is reflected rather than "*" (the two are exclusive).
const string DevCors = "dev-cors";
builder.Services.AddCors(options => options.AddPolicy(DevCors, policy =>
    policy.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

app.UseCors(DevCors);

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapVersionEndpoints();
app.MapAuthEndpoints();
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

    // The Identity bounded context (4.1) is a separate context/history on the same database.
    var identityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
    identityDb.Database.Migrate();

    // First-run escape hatch (4.1): seed a bootstrap admin (and grant it the Default org) when
    // configured, so a freshly deployed app has a user able to reach the seeded data. No-op when
    // unconfigured; idempotent, so it's safe on every startup. Tests don't rely on this.
    var bootstrapEmail = builder.Configuration["Auth:BootstrapAdmin:Email"];
    var bootstrapPassword = builder.Configuration["Auth:BootstrapAdmin:Password"];
    if (!string.IsNullOrWhiteSpace(bootstrapEmail) && !string.IsNullOrWhiteSpace(bootstrapPassword))
    {
        var users = scope.ServiceProvider.GetRequiredService<IUserDirectory>();
        var displayName = builder.Configuration["Auth:BootstrapAdmin:DisplayName"] ?? "Administrator";
        await IdentitySeeder.SeedBootstrapAdminAsync(users, bootstrapEmail, displayName, bootstrapPassword);
    }
}

app.Run();

// Exposed so the test host (WebApplicationFactory<Program>) can boot the app.
public partial class Program;
