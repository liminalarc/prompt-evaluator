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
        builder.Property(r => r.PromptId).IsRequired();
        builder.Property(r => r.PromptVersionId).IsRequired();
        builder.Property(r => r.DatasetId).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();

        // FixtureRuns are part of the aggregate — an owned collection loaded eagerly with the run.
        builder.OwnsMany(r => r.Results, fr =>
        {
            fr.ToTable("fixture_runs");
            fr.WithOwner().HasForeignKey("EvalRunId");
            fr.HasKey(x => x.Id);
            fr.Property(x => x.Id).ValueGeneratedNever();
            fr.Property(x => x.FixtureId).IsRequired();
            fr.Property(x => x.ModelOutput).IsRequired();
            fr.Property(x => x.LatencyMs).IsRequired();
            fr.Property(x => x.CostUsd);

            // Each FixtureRun owns its Scores; each Score owns its ScorerDescriptor (flattened to columns).
            fr.OwnsMany(x => x.Scores, sc =>
            {
                sc.ToTable("scores");
                sc.WithOwner().HasForeignKey("FixtureRunId");
                sc.HasKey(x => x.Id);
                sc.Property(x => x.Id).ValueGeneratedNever();
                sc.Property(x => x.Value).IsRequired();
                sc.Property(x => x.Passed);
                sc.Property(x => x.Detail);

                sc.OwnsOne(x => x.Scorer, d =>
                {
                    d.Property(p => p.Kind).HasConversion<string>().HasColumnName("scorer_kind").IsRequired();
                    d.Property(p => p.Config).HasColumnName("scorer_config").IsRequired();
                    d.Property(p => p.JudgeModel).HasColumnName("judge_model");
                    d.Ignore(p => p.Identity);
                });
                sc.Navigation(x => x.Scorer).IsRequired();
            });

            fr.Navigation(x => x.Scores)
                .HasField("_scores")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Navigation(r => r.Results)
            .HasField("_results")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
