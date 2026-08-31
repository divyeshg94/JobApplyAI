using JobApplyAi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobApplyAi.Infrastructure.Data.Configurations;

public class PollRunLogConfiguration : IEntityTypeConfiguration<PollRunLog>
{
    public void Configure(EntityTypeBuilder<PollRunLog> builder)
    {
        builder.Property(l => l.ErrorMessage).HasMaxLength(4000);

        builder.HasIndex(l => l.StartedAtUtc);

        builder.HasOne(l => l.JobSourceSubscription)
            .WithMany()
            .HasForeignKey(l => l.JobSourceSubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
