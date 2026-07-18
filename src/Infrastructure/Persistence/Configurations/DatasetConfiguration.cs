using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class DatasetConfiguration : IEntityTypeConfiguration<Dataset>
{
    public void Configure(EntityTypeBuilder<Dataset> builder)
    {
        builder.ToTable("datasets");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.PromptId).IsRequired();
        builder.Property(d => d.Name).IsRequired();
        builder.Property(d => d.Description);

        // A dataset lives with exactly one prompt (1.7); deleting the prompt takes its datasets
        // with it (they belong together).
        builder.HasOne<Prompt>()
            .WithMany()
            .HasForeignKey(d => d.PromptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.PromptId);

        // Fixtures are part of the aggregate — an owned collection, never queried on its own.
        // EF loads it eagerly with the dataset, so the repository needs no explicit Include.
        builder.OwnsMany(d => d.Fixtures, f =>
        {
            f.ToTable("fixtures");
            f.WithOwner().HasForeignKey("DatasetId");
            f.HasKey(x => x.Id);
            f.Property(x => x.Id).ValueGeneratedNever();
            f.Property(x => x.Origin).HasConversion<string>().IsRequired();
            f.Property(x => x.Label);
            f.Property(x => x.Description);
            f.Property(x => x.Input).IsRequired();
            f.Property(x => x.UpstreamContext);
            f.Property(x => x.ExpectedOutput);
            f.Property(x => x.SeedFixtureId);
            f.Property(x => x.CreatedAt).IsRequired();
        });

        // Read/write fixtures through the aggregate's private backing field, not the
        // read-only Fixtures navigation.
        builder.Navigation(d => d.Fixtures)
            .HasField("_fixtures")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
