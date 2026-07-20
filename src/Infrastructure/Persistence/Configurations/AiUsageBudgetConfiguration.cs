using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class AiUsageBudgetConfiguration : IEntityTypeConfiguration<AiUsageBudget>
{
    public void Configure(EntityTypeBuilder<AiUsageBudget> builder)
    {
        builder.ToTable("ai_usage_budgets");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.Scope).HasConversion<string>().IsRequired();
        builder.Property(b => b.ScopeValue);
        builder.Property(b => b.LimitUsd).IsRequired();
        builder.Property(b => b.Period).HasConversion<string>().IsRequired();
        builder.Property(b => b.AlertThresholdPercent).IsRequired();
        builder.Property(b => b.CreatedAt).IsRequired();
    }
}
