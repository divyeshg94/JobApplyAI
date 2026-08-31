using JobApplyAi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobApplyAi.Infrastructure.Data.Configurations;

public class JobSourceSubscriptionConfiguration : IEntityTypeConfiguration<JobSourceSubscription>
{
    public void Configure(EntityTypeBuilder<JobSourceSubscription> builder)
    {
        builder.Property(s => s.ConfigJson).HasColumnType("json");
        builder.Property(s => s.DisplayName).HasMaxLength(200);
        builder.Property(s => s.LastPollError).HasMaxLength(2000);

        builder.HasIndex(s => new { s.UserId, s.IsEnabled });

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
