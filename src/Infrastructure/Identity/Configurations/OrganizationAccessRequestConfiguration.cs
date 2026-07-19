using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Identity.Configurations;

internal sealed class OrganizationAccessRequestConfiguration : IEntityTypeConfiguration<OrganizationAccessRequest>
{
    public void Configure(EntityTypeBuilder<OrganizationAccessRequest> builder)
    {
        builder.ToTable("organization_access_requests");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.RequesterId).IsRequired();
        builder.Property(r => r.OrganizationId).IsRequired();
        builder.Property(r => r.RequestedRole).HasConversion<string>().IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();

        // At most one OPEN (Pending) request per (requester, org) — a partial unique index backs the
        // "no duplicate open request" rule at the DB level; a decided request never blocks a re-request.
        builder.HasIndex(r => new { r.RequesterId, r.OrganizationId })
            .IsUnique()
            .HasFilter("\"Status\" = 'Pending'");
    }
}
