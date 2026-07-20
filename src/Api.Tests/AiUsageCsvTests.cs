using Api.AiUsage;
using Application.AiUsage;
using Domain;

namespace Api.Tests;

/// <summary>
/// Unit tests for the AI-usage CSV export renderer (6.1.T3). Covers RFC-4180 quoting and the
/// formula-injection guard (6.1 security review): an attacker-influenced cell (e.g. an org member's
/// unvalidated model id) that begins with a spreadsheet formula trigger must not execute when an admin
/// opens the export. Pure — no DB.
/// </summary>
public sealed class AiUsageCsvTests
{
    private static AiUsageCall Call(string model, string? requestId = null) => new(
        Guid.NewGuid(), DateTimeOffset.Parse("2026-07-19T10:00:00Z"), AiUsageFeature.SubjectExecution,
        model, 100, 50, 0, 0, 0.5m, null, null, AiUsageStatus.Success, 90, requestId);

    private static string DataRow(string csv) =>
        csv.Split('\n', StringSplitOptions.RemoveEmptyEntries)[1]; // [0] is the header

    [Theory]
    [InlineData("=HYPERLINK(\"http://evil\",\"x\")")]
    [InlineData("+1+1")]
    [InlineData("-2+3")]
    [InlineData("@SUM(A1:A9)")]
    public void Neutralizes_formula_triggers_in_an_attacker_influenced_field(string payload)
    {
        var csv = AiUsageCsv.Build(new[] { Call(payload) });

        // The dangerous cell is prefixed with a single quote so the spreadsheet treats it as text.
        // (The `=...` / comma-bearing payloads are also RFC-4180 quoted; the guard runs first.)
        Assert.Contains("'" + payload[0], DataRow(csv));
        Assert.DoesNotContain($",{payload[0]}", DataRow(csv)); // never a bare leading trigger after a delimiter
    }

    [Fact]
    public void Leaves_an_ordinary_model_id_untouched()
    {
        var csv = AiUsageCsv.Build(new[] { Call("claude-opus-4-8") });

        Assert.Contains("claude-opus-4-8", DataRow(csv));
        Assert.DoesNotContain("'claude", csv);
    }

    [Fact]
    public void Quotes_a_comma_bearing_field_per_rfc_4180()
    {
        var csv = AiUsageCsv.Build(new[] { Call("model,with,commas") });

        Assert.Contains("\"model,with,commas\"", DataRow(csv));
    }
}
