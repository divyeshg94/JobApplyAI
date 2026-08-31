using JobApplyAi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobApplyAi.Infrastructure.Data.Configurations;

public class MatchResultConfiguration : IEntityTypeConfiguration<MatchResult>
{
    public void Configure(EntityTypeBuilder<MatchResult> builder)
    {
        builder.HasIndex(m => new { m.UserId, m.JobPostingId }).IsUnique();
        builder.HasIndex(m => new { m.UserId, m.Status });

        // User→MatchResult and User→CandidateProfile→MatchResult would form multiple cascade
        // paths, which SQL Server rejects — only the JobPosting FK cascades.
        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.CandidateProfile)
            .WithMany()
            .HasForeignKey(m => m.CandidateProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.JobPosting)
            .WithMany()
            .HasForeignKey(m => m.JobPostingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
