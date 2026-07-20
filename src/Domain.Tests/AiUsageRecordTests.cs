namespace Domain.Tests;

public class AiUsageRecordTests
{
    private static readonly DateTimeOffset When = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    private static AiUsageRecord Create(
        string model = "claude-opus-4-8",
        AiUsageFeature feature = AiUsageFeature.SubjectExecution,
        AiUsageStatus status = AiUsageStatus.Success,
        int inputTokens = 1000,
        int outputTokens = 500,
        int cacheCreationTokens = 0,
        int cacheReadTokens = 0,
        int latencyMs = 512,
        int? maxTokens = 4096)
        => AiUsageRecord.Create(
            model, feature, status,
            organizationId: Guid.NewGuid(), userId: Guid.NewGuid(),
            inputTokens, outputTokens, cacheCreationTokens, cacheReadTokens,
            latencyMs, maxTokens, requestId: "req_abc123", occurredAt: When);

    [Fact]
    public void Create_captures_every_field()
    {
        var org = Guid.NewGuid();
        var user = Guid.NewGuid();

        var record = AiUsageRecord.Create(
            "claude-opus-4-8", AiUsageFeature.LlmJudge, AiUsageStatus.Success,
            organizationId: org, userId: user,
            inputTokens: 1200, outputTokens: 340, cacheCreationTokens: 100, cacheReadTokens: 900,
            latencyMs: 777, maxTokens: 8192, requestId: "req_xyz", occurredAt: When);

        Assert.NotEqual(Guid.Empty, record.Id);
        Assert.Equal("claude-opus-4-8", record.Model);
        Assert.Equal(AiUsageFeature.LlmJudge, record.Feature);
        Assert.Equal(AiUsageStatus.Success, record.Status);
        Assert.Equal(org, record.OrganizationId);
        Assert.Equal(user, record.UserId);
        Assert.Equal(1200, record.InputTokens);
        Assert.Equal(340, record.OutputTokens);
        Assert.Equal(100, record.CacheCreationTokens);
        Assert.Equal(900, record.CacheReadTokens);
        Assert.Equal(777, record.LatencyMs);
        Assert.Equal(8192, record.MaxTokens);
        Assert.Equal("req_xyz", record.RequestId);
        Assert.Equal(When, record.OccurredAt);
    }

    [Fact]
    public void Create_allows_null_attribution_request_id_and_max_tokens()
    {
        // A call can fail before attribution/usage is known — the ledger still records the waste.
        var record = AiUsageRecord.Create(
            "unknown", AiUsageFeature.SyntheticGeneration, AiUsageStatus.Error,
            organizationId: null, userId: null,
            inputTokens: 0, outputTokens: 0, cacheCreationTokens: 0, cacheReadTokens: 0,
            latencyMs: 0, maxTokens: null, requestId: null, occurredAt: When);

        Assert.Null(record.OrganizationId);
        Assert.Null(record.UserId);
        Assert.Null(record.RequestId);
        Assert.Null(record.MaxTokens);
        Assert.Equal(AiUsageStatus.Error, record.Status);
    }

    [Fact]
    public void Create_rejects_a_null_or_blank_model()
    {
        Assert.Throws<ArgumentException>(() => Create(model: ""));
        Assert.Throws<ArgumentException>(() => Create(model: "   "));
    }

    [Theory]
    [InlineData(-1, 0, 0, 0, 0)]
    [InlineData(0, -1, 0, 0, 0)]
    [InlineData(0, 0, -1, 0, 0)]
    [InlineData(0, 0, 0, -1, 0)]
    [InlineData(0, 0, 0, 0, -1)]
    public void Create_rejects_negative_tokens_or_latency(int input, int output, int cacheCreate, int cacheRead, int latency)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(
            inputTokens: input, outputTokens: output, cacheCreationTokens: cacheCreate,
            cacheReadTokens: cacheRead, latencyMs: latency));
    }

    [Fact]
    public void Create_rejects_a_negative_max_tokens()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(maxTokens: -1));
    }
}
