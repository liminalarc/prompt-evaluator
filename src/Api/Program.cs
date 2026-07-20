using Api.Admin;
using Api.AiUsage;
using Api.Analytics;
using Api.Auth;
using Api.Datasets;
using Api.EvalRuns;
using Api.Folders;
using Api.Models;
using Api.Organizations;
using Api.Prompts;
using Api.Seam;
using Api.Version;
using Application.Ports;
using Application.Analytics;
using Application.Datasets;
using Application.EvalRuns;
using Application.Folders;
using Application.Models;
using Application.Prompts;
using Infrastructure;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var postgres = builder.Configuration.GetConnectionString("Postgres") ?? "";
var evalRunnerBaseUrl = builder.Configuration["EvalRunner:BaseUrl"] ?? "http://localhost:8000";
// eval-runner is an internal trusted service (4.1): the shared service token authenticates the
// backend to it. Unset in dev/CI/tests, where eval-runner stays open.
var evalRunnerServiceToken = builder.Configuration["EvalRunner:ServiceToken"];

builder.Services.AddInfrastructure(postgres, evalRunnerBaseUrl, evalRunnerServiceToken);

// AI-usage pricing (6.1.T2): bind the optional `AiUsagePricing` config section over the built-in
// defaults seeded in AddInfrastructure, so rates/version can be overridden without a code change.
builder.Services.AddOptions<Infrastructure.Pricing.AiUsagePricingOptions>()
    .Bind(builder.Configuration.GetSection("AiUsagePricing"));

// Identity token providers (password-reset tokens) and the SignInManager live in the ASP.NET Core
// shared framework, so they're added here at the composition root rather than in the (framework-light)
// Infrastructure. SignInManager (3.2) issues the auth cookie with a security-stamp claim.
new IdentityBuilder(typeof(AppUser), builder.Services)
    .AddDefaultTokenProviders()
    .AddSignInManager();

// The cookie principal also carries email + display-name claims (3.2 / 4.1 /me).
builder.Services.AddScoped<IUserClaimsPrincipalFactory<AppUser>, AppUserClaimsPrincipalFactory>();

// Live-session invalidation on password reset (3.2): the SecurityStampValidator re-checks the user's
// security stamp on every request (ValidationInterval = Zero), so a reset — which rotates the stamp —
// rejects still-held cookies immediately.
builder.Services.AddScoped<ISecurityStampValidator, SecurityStampValidator<AppUser>>();
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);

// Data-Protection keys persist to Postgres (3.2) so the auth cookie is valid across App Runner
// replicas (a per-process key ring would reject cookies issued by another instance). Skipped with no
// database configured (e.g. the bare /health + SPA integration tests) — those never issue a cookie.
if (!string.IsNullOrWhiteSpace(postgres))
    builder.Services.AddDataProtection()
        .SetApplicationName("litmus-ai")
        .PersistKeysToDbContext<AppIdentityDbContext>();

builder.Services.AddScoped<CreatePromptHandler>();
builder.Services.AddScoped<AddPromptVersionHandler>();
builder.Services.AddScoped<EditPromptVersionHandler>();
builder.Services.AddScoped<SetCurrentVersionHandler>();
builder.Services.AddScoped<CreateFolderHandler>();
builder.Services.AddScoped<RenameFolderHandler>();
builder.Services.AddScoped<MoveFolderHandler>();
builder.Services.AddScoped<MovePromptHandler>();
builder.Services.AddScoped<CreateModelHandler>();
builder.Services.AddScoped<UpdateModelHandler>();
builder.Services.AddScoped<SetModelActiveHandler>();
builder.Services.AddScoped<CreateDatasetHandler>();
builder.Services.AddScoped<CaptureFixturesHandler>();
builder.Services.AddScoped<EditFixtureHandler>();
builder.Services.AddScoped<GenerateSyntheticFixturesHandler>();
builder.Services.AddScoped<ConfigureDatasetScorersHandler>();
builder.Services.AddScoped<RunEvaluationHandler>();
builder.Services.AddScoped<TrendAnalyticsHandler>();
builder.Services.AddScoped<CompositeTrendHandler>();
builder.Services.AddScoped<RegressionAnalyticsHandler>();
builder.Services.AddScoped<ComparisonAnalyticsHandler>();
builder.Services.AddScoped<VarianceAnalyticsHandler>();
builder.Services.AddScoped<VersionStatusHandler>();
builder.Services.AddScoped<BackportArtifactHandler>();
builder.Services.AddScoped<Application.AiUsage.BudgetStatusHandler>();
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
        // Re-validate the security stamp on each request (3.2) — rejects cookies after a password reset.
        options.Events.OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync;
    })
    // On rejection the SecurityStampValidator also signs out the two-factor-remember-me scheme, so it
    // must be a registered cookie scheme even though we don't use 2FA. (AddIdentityCookies registers
    // this for you; we wire the application cookie by hand, so register it explicitly.)
    .AddCookie(IdentityConstants.TwoFactorRememberMeScheme);
builder.Services.AddAuthorization();

// Allow the Angular dev server to reach the API during per-process development. Credentials are
// enabled for the auth cookie, so the origin is reflected rather than "*" (the two are exclusive).
const string DevCors = "dev-cors";
builder.Services.AddCors(options => options.AddPolicy(DevCors, policy =>
    policy.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

app.UseCors(DevCors);

// Single-origin deploy (3.2): outside Development the API also serves the built Angular SPA from
// wwwroot (the `litmus-ai` App Runner image bundles it — no nginx). In Development the SPA is owned
// by `ng serve` / the compose nginx, so static serving stays off and the API is framework-only.
if (!app.Environment.IsDevelopment())
    app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapVersionEndpoints();
app.MapAuthEndpoints();
app.MapEchoEndpoints();
app.MapOrganizationEndpoints();
app.MapPromptEndpoints();
app.MapModelEndpoints();
app.MapAdminUserEndpoints();
app.MapAdminOrganizationEndpoints();
app.MapFolderEndpoints();
app.MapDatasetEndpoints();
app.MapEvalHarnessEndpoints();
app.MapAnalyticsEndpoints();
app.MapAiUsageEndpoints();

// SPA client-side routes (e.g. /prompts, /analytics) fall back to index.html outside Development.
// Lowest route priority, so the /api/* endpoints and /health, /version above keep their responses.
if (!app.Environment.IsDevelopment())
    app.MapFallbackToFile("index.html");

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
