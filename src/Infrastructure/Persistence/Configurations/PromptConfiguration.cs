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
        builder.Property(p => p.OrganizationId).IsRequired();
        builder.Property(p => p.Name).IsRequired();
        builder.Property(p => p.Description);
        builder.Property(p => p.FolderId);

        // The "Current in source" marker (1.16): which version the source app runs. A plain nullable
        // id column (no DB FK to the owned prompt_versions — same reference-by-id style as
        // EvalRun.PromptVersionId); the aggregate enforces it points at a version it owns.
        builder.Property(p => p.CurrentVersionId);
        builder.Property(p => p.CurrentVersionSha);
        builder.Property(p => p.CurrentVersionSetAt);

        // A prompt belongs to an organization (1.9) — the 4.1 permission boundary. Deleting an org
        // takes its prompts with it.
        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(p => p.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.OrganizationId);

        // A prompt is filed into a folder (1.7); null = unfiled. Deleting a folder unfiles its
        // prompts (SET NULL) rather than deleting them.
        builder.HasOne<Folder>()
            .WithMany()
            .HasForeignKey(p => p.FolderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(p => p.FolderId);

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
