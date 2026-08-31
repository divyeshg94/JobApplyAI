using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobApplyAi.Infrastructure.Data.Configurations;

public class ApplicationConfiguration : IEntityTypeConfiguration<Domain.Entities.Application>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Application> builder)
    {
        builder.Property(a => a.TailoredResumeBlobUrl).HasMaxLength(1000);
        builder.Property(a => a.TailoredCoverLetterBlobUrl).HasMaxLength(1000);
        builder.Property(a => a.Notes).HasMaxLength(4000);

        builder.HasIndex(a => new { a.UserId, a.Status });

        // Same multiple-cascade-path constraint as MatchResult — only the MatchResult FK cascades.
        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.MatchResult)
            .WithMany()
            .HasForeignKey(a => a.MatchResultId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
