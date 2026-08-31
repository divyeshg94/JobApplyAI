using JobApplyAi.Domain.Entities;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobApplyAi.Infrastructure.Data.Configurations;

public class JobPostingConfiguration : IEntityTypeConfiguration<JobPosting>
{
    public void Configure(EntityTypeBuilder<JobPosting> builder)
    {
        builder.Property(j => j.ExternalJobId).HasMaxLength(200);
        builder.Property(j => j.Title).HasMaxLength(300);
        builder.Property(j => j.CompanyName).HasMaxLength(200);
        builder.Property(j => j.LocationText).HasMaxLength(300);
        builder.Property(j => j.ApplyUrl).HasMaxLength(1000);
        builder.Property(j => j.WorkLocationCountry).HasMaxLength(10);
        builder.Property(j => j.RawJsonPayload).HasColumnType("json");

        builder.Property<SqlVector<float>?>(AppDbContext.JobEmbeddingColumn)
            .HasColumnType($"vector({AppDbContext.EmbeddingDimensions})");

        // Dedup key — same-source re-polls only; cross-source duplicates are a known v1 limitation.
        builder.HasIndex(j => new { j.Source, j.ExternalJobId }).IsUnique();
    }
}
