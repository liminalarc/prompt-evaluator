using Api.Auth;
using Application.AiUsage;
using Domain;

namespace Api.AiUsage;

/// <summary>
/// Admin → AI Usage endpoints (6.1.T3/T4): filter + aggregate + drill + export over the usage ledger.
/// A workspace-wide management surface — every route is gated to the global-admin flag
/// (<see cref="OrgAccess.IsGlobalAdminAsync"/>), matching the Model Catalog / admin surfaces.
/// </summary>
public static class AiUsageEndpoints
{
    public static IEndpointRouteBuilder MapAiUsageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/ai-usage").RequireAuthorization();

        group.MapGet("/summary", async (
            HttpRequest req, IAiUsageQueries queries, OrgAccess access, CancellationToken ct) =>
        {
            if (!await access.IsGlobalAdminAsync(ct)) return Results.Forbid();
            if (!TryBuildFilter(req, out var filter, out var error)) return Results.BadRequest(new { error });
            return Results.Ok(await queries.SummaryAsync(filter, ct));
        });

        group.MapGet("/breakdown", async (
            string dimension, int? topN, HttpRequest req, IAiUsageQueries queries, OrgAccess access, CancellationToken ct) =>
        {
            if (!await access.IsGlobalAdminAsync(ct)) return Results.Forbid();
            if (!Enum.TryParse<AiUsageDimension>(dimension, ignoreCase: true, out var dim))
                return Results.BadRequest(new { error = $"Unknown dimension '{dimension}'." });
            if (!TryBuildFilter(req, out var filter, out var error)) return Results.BadRequest(new { error });
            return Results.Ok(await queries.BreakdownAsync(filter, dim, topN, ct));
        });

        group.MapGet("/timeseries", async (
            string? period, HttpRequest req, IAiUsageQueries queries, OrgAccess access, CancellationToken ct) =>
        {
            if (!await access.IsGlobalAdminAsync(ct)) return Results.Forbid();
            if (!Enum.TryParse<AiUsagePeriod>(period ?? "Day", ignoreCase: true, out var p))
                return Results.BadRequest(new { error = $"Unknown period '{period}'." });
            if (!TryBuildFilter(req, out var filter, out var error)) return Results.BadRequest(new { error });
            return Results.Ok(await queries.TimeSeriesAsync(filter, p, ct));
        });

        group.MapGet("/calls", async (
            int? page, int? pageSize, HttpRequest req, IAiUsageQueries queries, OrgAccess access, CancellationToken ct) =>
        {
            if (!await access.IsGlobalAdminAsync(ct)) return Results.Forbid();
            if (!TryBuildFilter(req, out var filter, out var error)) return Results.BadRequest(new { error });
            return Results.Ok(await queries.CallsAsync(filter, page ?? 1, pageSize ?? 50, ct));
        });

        group.MapGet("/export.csv", async (
            HttpContext http, IAiUsageQueries queries, OrgAccess access, CancellationToken ct) =>
        {
            if (!await access.IsGlobalAdminAsync(ct)) return Results.Forbid();
            if (!TryBuildFilter(http.Request, out var filter, out var error)) return Results.BadRequest(new { error });
            var calls = await queries.ExportAsync(filter, ct);
            var csv = AiUsageCsv.Build(calls);
            http.Response.Headers.ContentDisposition = "attachment; filename=\"ai-usage.csv\"";
            return Results.Text(csv, "text/csv");
        });

        return app;
    }

    // Parses the shared filter query params (comma-separated lists). Invalid enum/guid tokens → 400.
    private static bool TryBuildFilter(HttpRequest req, out AiUsageFilter filter, out string? error)
    {
        filter = new AiUsageFilter();
        error = null;
        var q = req.Query;

        DateTimeOffset? from = null, to = null;
        if (q.TryGetValue("from", out var fromRaw) && !string.IsNullOrWhiteSpace(fromRaw) &&
            !TryParseDate(fromRaw!, "from", out from, out error)) return false;
        if (q.TryGetValue("to", out var toRaw) && !string.IsNullOrWhiteSpace(toRaw) &&
            !TryParseDate(toRaw!, "to", out to, out error)) return false;

        var models = Split(q["models"]);

        if (!TryParseEnums<AiUsageFeature>(q["features"], "feature", out var features, out error)) return false;
        if (!TryParseEnums<AiUsageStatus>(q["statuses"], "status", out var statuses, out error)) return false;
        if (!TryParseGuids(q["users"], "user", out var users, out error)) return false;
        if (!TryParseGuids(q["orgs"], "org", out var orgs, out error)) return false;

        filter = new AiUsageFilter(from, to, models, features, users, orgs, statuses);
        return true;
    }

    private static bool TryParseDate(string raw, string field, out DateTimeOffset? value, out string? error)
    {
        value = null; error = null;
        if (DateTimeOffset.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed))
        {
            value = parsed;
            return true;
        }
        error = $"Invalid {field} date '{raw}'.";
        return false;
    }

    private static List<string>? Split(Microsoft.Extensions.Primitives.StringValues raw)
    {
        var items = raw.SelectMany(v => (v ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToList();
        return items.Count == 0 ? null : items;
    }

    private static bool TryParseEnums<TEnum>(
        Microsoft.Extensions.Primitives.StringValues raw, string field, out IReadOnlyList<TEnum>? values, out string? error)
        where TEnum : struct, Enum
    {
        values = null; error = null;
        var tokens = Split(raw);
        if (tokens is null) return true;
        var parsed = new List<TEnum>();
        foreach (var t in tokens)
        {
            if (!Enum.TryParse<TEnum>(t, ignoreCase: true, out var e))
            {
                error = $"Unknown {field} '{t}'.";
                return false;
            }
            parsed.Add(e);
        }
        values = parsed;
        return true;
    }

    private static bool TryParseGuids(
        Microsoft.Extensions.Primitives.StringValues raw, string field, out IReadOnlyList<Guid>? values, out string? error)
    {
        values = null; error = null;
        var tokens = Split(raw);
        if (tokens is null) return true;
        var parsed = new List<Guid>();
        foreach (var t in tokens)
        {
            if (!Guid.TryParse(t, out var g))
            {
                error = $"Invalid {field} id '{t}'.";
                return false;
            }
            parsed.Add(g);
        }
        values = parsed;
        return true;
    }
}
