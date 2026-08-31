using JobApplyAi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobApplyAi.Infrastructure.Data.Configurations;

public class ProfileWorkExperienceConfiguration : IEntityTypeConfiguration<ProfileWorkExperience>
{
    public void Configure(EntityTypeBuilder<ProfileWorkExperience> builder)
    {
        builder.Property(e => e.Company).HasMaxLength(200);
        builder.Property(e => e.Title).HasMaxLength(200);
        builder.Property(e => e.LocationText).HasMaxLength(300);
    }
}

public class ProfileEducationConfiguration : IEntityTypeConfiguration<ProfileEducation>
{
    public void Configure(EntityTypeBuilder<ProfileEducation> builder)
    {
        builder.Property(e => e.Institution).HasMaxLength(300);
        builder.Property(e => e.Degree).HasMaxLength(200);
        builder.Property(e => e.FieldOfStudy).HasMaxLength(200);
    }
}

public class ProfileSkillConfiguration : IEntityTypeConfiguration<ProfileSkill>
{
    public void Configure(EntityTypeBuilder<ProfileSkill> builder)
    {
        builder.Property(s => s.Name).HasMaxLength(150);
        builder.Property(s => s.Category).HasMaxLength(100);
    }
}

public class ProfileExcludedCompanyConfiguration : IEntityTypeConfiguration<ProfileExcludedCompany>
{
    public void Configure(EntityTypeBuilder<ProfileExcludedCompany> builder)
    {
        builder.Property(c => c.CompanyName).HasMaxLength(200);
    }
}
