using JobApplyAi.Domain.Entities;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobApplyAi.Infrastructure.Data.Configurations;

public class CandidateProfileConfiguration : IEntityTypeConfiguration<CandidateProfile>
{
    public void Configure(EntityTypeBuilder<CandidateProfile> builder)
    {
        builder.Property(p => p.FullName).HasMaxLength(200);
        builder.Property(p => p.Email).HasMaxLength(320);
        builder.Property(p => p.Phone).HasMaxLength(50);
        builder.Property(p => p.LocationText).HasMaxLength(300);
        builder.Property(p => p.LinkedInUrl).HasMaxLength(500);
        builder.Property(p => p.PortfolioUrl).HasMaxLength(500);
        builder.Property(p => p.RawResumeBlobUrl).HasMaxLength(1000);
        builder.Property(p => p.RawResumeFileName).HasMaxLength(300);
        builder.Property(p => p.RawResumeContentType).HasMaxLength(100);
        builder.Property(p => p.RequiredCountry).HasMaxLength(10);

        builder.Property<SqlVector<float>?>(AppDbContext.ProfileEmbeddingColumn)
            .HasColumnType($"vector({AppDbContext.EmbeddingDimensions})");

        builder.HasIndex(p => new { p.UserId, p.Status });

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.WorkExperiences)
            .WithOne(e => e.CandidateProfile)
            .HasForeignKey(e => e.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Educations)
            .WithOne(e => e.CandidateProfile)
            .HasForeignKey(e => e.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Skills)
            .WithOne(s => s.CandidateProfile)
            .HasForeignKey(s => s.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.ExcludedCompanies)
            .WithOne(c => c.CandidateProfile)
            .HasForeignKey(c => c.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
