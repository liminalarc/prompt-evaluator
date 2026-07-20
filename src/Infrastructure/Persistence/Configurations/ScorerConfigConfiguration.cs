using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class ScorerConfigConfiguration : IEntityTypeConfiguration<ScorerConfig>
{
    public void Configure(EntityTypeBuilder<ScorerConfig> builder)
    {
        builder.ToTable("scorer_configs");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.DatasetId).IsRequired();
        // Per-scorer composite weight (2.9); default 1.0 so pre-existing rows are equally weighted.
        builder.Property(c => c.Weight).IsRequired().HasDefaultValue(1.0);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.HasIndex(c => c.DatasetId);

        // The scorer's identity (kind + config + judge model) is flattened onto the row.
        builder.OwnsOne(c => c.Scorer, d =>
        {
            d.Property(p => p.Kind).HasConversion<string>().HasColumnName("scorer_kind").IsRequired();
            d.Property(p => p.Config).HasColumnName("scorer_config").IsRequired();
            d.Property(p => p.JudgeModel).HasColumnName("judge_model");
            d.Ignore(p => p.Identity);
        });
        builder.Navigation(c => c.Scorer).IsRequired();
    }
}
