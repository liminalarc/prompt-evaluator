using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class EvalRunConfiguration : IEntityTypeConfiguration<EvalRun>
{
    public void Configure(EntityTypeBuilder<EvalRun> builder)
    {
        builder.ToTable("eval_runs");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Prompt).IsRequired();
        builder.Property(r => r.Output).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();
    }
}
