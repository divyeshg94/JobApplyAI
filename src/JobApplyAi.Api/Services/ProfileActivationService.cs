using JobApplyAi.Domain;
using JobApplyAi.Domain.Entities;
using JobApplyAi.Domain.Seed;
using JobApplyAi.Infrastructure.Data;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace JobApplyAi.Api.Services;

/// <summary>
/// Confirms a reviewed profile: generates its embedding from the user-approved text, marks it
/// Active, supersedes any previous Active profile. Shared by the API endpoint and the Blazor UI.
/// </summary>
public static class ProfileActivationService
{
    public static async Task ActivateAsync(
        AppDbContext db,
        Guid profileId,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        CancellationToken ct = default)
    {
        var profile = await db.CandidateProfiles
            .Include(p => p.WorkExperiences)
            .Include(p => p.Skills)
            .FirstOrDefaultAsync(p => p.Id == profileId, ct)
            ?? throw new InvalidOperationException($"Profile {profileId} not found.");

        if (profile.Status is not (ProfileStatus.NeedsReview or ProfileStatus.Active))
        {
            throw new InvalidOperationException($"Profile is {profile.Status}, not reviewable.");
        }

        var embedding = await embeddingGenerator.GenerateAsync(BuildEmbeddingText(profile), cancellationToken: ct);
        db.Entry(profile).Property<SqlVector<float>?>(AppDbContext.ProfileEmbeddingColumn).CurrentValue =
            new SqlVector<float>(embedding.Vector);

        var previouslyActive = await db.CandidateProfiles
            .Where(p => p.UserId == profile.UserId && p.Status == ProfileStatus.Active && p.Id != profile.Id)
            .ToListAsync(ct);
        foreach (var old in previouslyActive)
        {
            old.Status = ProfileStatus.Superseded;
        }

        profile.Status = ProfileStatus.Active;
        profile.ReviewedAtUtc = DateTimeOffset.UtcNow;

        // Bootstrap the notify address from the resume's own contact email, but only while it's
        // still the seed placeholder — never overwrite an address the user set deliberately
        // (their notify inbox and their on-resume contact email aren't guaranteed to be the same).
        if (!string.IsNullOrWhiteSpace(profile.Email))
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == profile.UserId, ct);
            if (user is not null && user.Email == SeedData.PlaceholderEmail)
            {
                user.Email = profile.Email;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    public static string BuildEmbeddingText(CandidateProfile profile)
    {
        var experiences = profile.WorkExperiences
            .Select(w => $"{w.Title} at {w.Company}: {w.DescriptionText}");
        var skills = string.Join(", ", profile.Skills.Select(s => s.Name));
        return $"""
            {profile.SummaryText}

            Experience:
            {string.Join("\n", experiences)}

            Skills: {skills}
            """;
    }
}
