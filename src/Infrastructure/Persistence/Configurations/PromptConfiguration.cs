using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class PromptConfiguration : IEntityTypeConfiguration<Prompt>
{
    public void Configure(EntityTypeBuilder<Prompt> builder)
    {
        builder.ToTable("prompts");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired();
        builder.Property(p => p.Description);

        // Version history is part of the aggregate — an owned collection, never queried on its
        // own. EF loads it eagerly with the prompt, so the repository needs no explicit Include.
        builder.OwnsMany(p => p.Versions, v =>
        {
            v.ToTable("prompt_versions");
            v.WithOwner().HasForeignKey("PromptId");
            v.HasKey(x => x.Id);
            v.Property(x => x.Id).ValueGeneratedNever();
            v.Property(x => x.VersionNumber).IsRequired();
            v.Property(x => x.Content).IsRequired();
            v.Property(x => x.TargetModel).IsRequired();
            v.Property(x => x.Label);
            v.Property(x => x.SourceApp);
            v.Property(x => x.CreatedAt).IsRequired();
        });

        // Read/write the history through the aggregate's private backing field, not the
        // read-only Versions navigation.
        builder.Navigation(p => p.Versions)
            .HasField("_versions")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
