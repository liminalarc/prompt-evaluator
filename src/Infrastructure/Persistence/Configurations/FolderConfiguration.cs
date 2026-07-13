using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class FolderConfiguration : IEntityTypeConfiguration<Folder>
{
    public void Configure(EntityTypeBuilder<Folder> builder)
    {
        builder.ToTable("folders");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.OrganizationId).IsRequired();
        builder.Property(f => f.Name).IsRequired();
        builder.Property(f => f.ParentId);

        // A folder belongs to an organization (1.9). Deleting an org takes its folders with it.
        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(f => f.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => f.OrganizationId);

        // Self-referencing tree. Restrict on delete: a folder with children can't be removed out
        // from under them (folder deletion isn't in 1.7's scope anyway).
        builder.HasOne<Folder>()
            .WithMany()
            .HasForeignKey(f => f.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(f => f.ParentId);
    }
}
