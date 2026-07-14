using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Identity.Configurations;

internal sealed class OrganizationMembershipConfiguration : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationMembership> builder)
    {
        builder.ToTable("organization_memberships");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.UserId).IsRequired();
        builder.Property(m => m.OrganizationId).IsRequired();
        builder.Property(m => m.Role).HasConversion<string>().IsRequired();

        // A user has at most one grant per organization; the pair is the natural lookup key.
        builder.HasIndex(m => new { m.UserId, m.OrganizationId }).IsUnique();
    }
}
