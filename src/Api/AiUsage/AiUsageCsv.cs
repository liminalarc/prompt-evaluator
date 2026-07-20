using System.Globalization;
using System.Text;
using Application.AiUsage;

namespace Api.AiUsage;

/// <summary>Renders the filtered AI-usage calls as RFC-4180 CSV (6.1.T3 export).</summary>
public static class AiUsageCsv
{
    private static readonly string[] Header =
    [
        "occurred_at", "feature", "model", "status",
        "input_tokens", "output_tokens", "cache_creation_tokens", "cache_read_tokens",
        "cost_usd", "latency_ms", "organization_id", "user_id", "request_id",
    ];

    public static string Build(IReadOnlyList<AiUsageCall> calls)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', Header));
        foreach (var c in calls)
        {
            sb.AppendLine(string.Join(',', new[]
            {
                Field(c.OccurredAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)),
                Field(c.Feature.ToString()),
                Field(c.Model),
                Field(c.Status.ToString()),
                Field(c.InputTokens.ToString(CultureInfo.InvariantCulture)),
                Field(c.OutputTokens.ToString(CultureInfo.InvariantCulture)),
                Field(c.CacheCreationTokens.ToString(CultureInfo.InvariantCulture)),
                Field(c.CacheReadTokens.ToString(CultureInfo.InvariantCulture)),
                Field(c.CostUsd?.ToString(CultureInfo.InvariantCulture) ?? ""),
                Field(c.LatencyMs.ToString(CultureInfo.InvariantCulture)),
                Field(c.OrganizationId?.ToString() ?? ""),
                Field(c.UserId?.ToString() ?? ""),
                Field(c.RequestId ?? ""),
            }));
        }
        return sb.ToString();
    }

    // Characters that make a spreadsheet treat a cell as a formula (Excel / Sheets / LibreOffice).
    private static readonly char[] FormulaTriggers = ['=', '+', '-', '@', '\t', '\r'];

    private static string Field(string value)
    {
        // Formula-injection guard: a cell whose text is attacker-influenced (e.g. `model` comes from an
        // org member's unvalidated TargetModel) can begin with `=`/`+`/`-`/`@`/tab/CR and execute as a
        // formula when an admin opens the export. Neutralize by prefixing a single quote (OWASP CSV
        // injection mitigation) before RFC-4180 quoting. Empty/numeric cells are unaffected.
        if (value.Length > 0 && Array.IndexOf(FormulaTriggers, value[0]) >= 0)
            value = "'" + value;

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
