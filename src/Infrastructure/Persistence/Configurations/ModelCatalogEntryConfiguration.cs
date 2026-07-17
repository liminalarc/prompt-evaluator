using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class ModelCatalogEntryConfiguration : IEntityTypeConfiguration<ModelCatalogEntry>
{
    public void Configure(EntityTypeBuilder<ModelCatalogEntry> builder)
    {
        builder.ToTable("model_catalog_entries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ModelId).IsRequired();
        builder.HasIndex(e => e.ModelId).IsUnique();

        builder.Property(e => e.DisplayName).IsRequired();
        builder.Property(e => e.Provider).HasConversion<string>().IsRequired();

        // Roles are flags, not a child table (no List<enum>/JSONB precedent; the set is fixed).
        builder.Property(e => e.CanSubject).IsRequired();
        builder.Property(e => e.CanJudge).IsRequired();
        builder.Property(e => e.CanGenerate).IsRequired();

        // Display-only pricing (1.13) — nullable; the eval-runner owns execution pricing.
        builder.Property(e => e.InputPricePerMTokUsd);
        builder.Property(e => e.OutputPricePerMTokUsd);

        builder.Property(e => e.IsActive).IsRequired();

        // Derived from the flags — not a stored column.
        builder.Ignore(e => e.Roles);
    }
}
