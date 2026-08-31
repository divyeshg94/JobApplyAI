using JobApplyAi.Domain.Entities;
using JobApplyAi.Domain.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobApplyAi.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.Email).HasMaxLength(320);
        builder.Property(u => u.DisplayName).HasMaxLength(200);

        builder.HasData(new User
        {
            Id = SeedData.DefaultUserId,
            Email = SeedData.PlaceholderEmail,
            DisplayName = "Default User",
            CreatedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        });
    }
}
