using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class AiUsageRecordConfiguration : IEntityTypeConfiguration<AiUsageRecord>
{
    public void Configure(EntityTypeBuilder<AiUsageRecord> builder)
    {
        builder.ToTable("ai_usage_records");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Model).IsRequired();
        builder.Property(r => r.Feature).HasConversion<string>().IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().IsRequired();
        builder.Property(r => r.OrganizationId);
        builder.Property(r => r.UserId);
        builder.Property(r => r.InputTokens).IsRequired();
        builder.Property(r => r.OutputTokens).IsRequired();
        builder.Property(r => r.CacheCreationTokens).IsRequired();
        builder.Property(r => r.CacheReadTokens).IsRequired();
        builder.Property(r => r.LatencyMs).IsRequired();
        builder.Property(r => r.MaxTokens);
        builder.Property(r => r.RequestId);
        builder.Property(r => r.OccurredAt).IsRequired();

        // Query slices (6.1.T3) filter by time and group by these dimensions — index the common ones.
        builder.HasIndex(r => r.OccurredAt);
        builder.HasIndex(r => new { r.OrganizationId, r.OccurredAt });
        builder.HasIndex(r => r.Feature);
        builder.HasIndex(r => r.Model);
    }
}
