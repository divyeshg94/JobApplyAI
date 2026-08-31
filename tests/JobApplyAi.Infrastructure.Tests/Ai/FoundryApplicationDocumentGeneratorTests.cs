using JobApplyAi.Domain;
using JobApplyAi.Domain.Entities;
using JobApplyAi.Infrastructure.Ai;

namespace JobApplyAi.Infrastructure.Tests.Ai;

public class FoundryApplicationDocumentGeneratorTests
{
    private static CandidateProfile MakeProfile() => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        FullName = "Jane Doe",
        SummaryText = "Senior engineer.",
        WorkExperiences =
        [
            new ProfileWorkExperience
            {
                Id = Guid.NewGuid(), CandidateProfileId = Guid.Empty, Company = "Acme", Title = "Engineer",
                StartDate = new DateOnly(2020, 1, 1), IsCurrent = true,
                DescriptionText = "Built the payments pipeline handling 1M requests/day.",
            },
        ],
        Skills = [new ProfileSkill { Id = Guid.NewGuid(), CandidateProfileId = Guid.Empty, Name = "C#" }],
    };

    private static JobPosting MakeJobPosting() => new()
    {
        Id = Guid.NewGuid(), Source = JobSource.Greenhouse, ExternalJobId = "1",
        Title = "Staff Engineer", CompanyName = "Widgets Inc", ApplyUrl = "https://example.com",
        FetchedAtUtc = DateTimeOffset.UtcNow, RawJsonPayload = "{}",
        DescriptionText = "Looking for a staff engineer to scale our platform.",
    };

    [Fact]
    public async Task GenerateAsync_uses_model_bullets_when_companies_and_titles_match_exactly()
    {
        const string reply =
            """
            {"resumeSummary": "Tailored summary.",
             "workExperiences": [{"company": "Acme", "title": "Engineer", "bullets": ["Scaled payments to 1M req/day."]}],
             "skillsOrdered": ["C#"],
             "coverLetterText": "Dear team,\n\nI would love to join.\n\nBest, Jane"}
            """;
        var generator = new FoundryApplicationDocumentGenerator(new FakeChatClient(reply));

        var result = await generator.GenerateAsync(MakeProfile(), MakeJobPosting(), CancellationToken.None);

        Assert.Equal("Tailored summary.", result.ResumeSummary);
        var work = Assert.Single(result.WorkExperiences);
        Assert.Equal("Acme", work.Company);
        Assert.Equal(["Scaled payments to 1M req/day."], work.Bullets);
        Assert.Equal(["C#"], result.SkillsOrdered);
    }

    [Fact]
    public async Task GenerateAsync_falls_back_to_original_text_when_model_invents_a_different_employer()
    {
        // The model hallucinated a company/title the candidate never worked at — this must never
        // reach the rendered resume. Falling back to the source profile's own text is the guard.
        const string reply =
            """
            {"resumeSummary": "Tailored summary.",
             "workExperiences": [{"company": "Globex", "title": "Principal Engineer", "bullets": ["Led a team of 50."]}],
             "skillsOrdered": ["C#"],
             "coverLetterText": "Dear team,\n\nI would love to join.\n\nBest, Jane"}
            """;
        var generator = new FoundryApplicationDocumentGenerator(new FakeChatClient(reply));
        var profile = MakeProfile();

        var result = await generator.GenerateAsync(profile, MakeJobPosting(), CancellationToken.None);

        var work = Assert.Single(result.WorkExperiences);
        Assert.Equal("Acme", work.Company); // the candidate's REAL employer, not "Globex"
        Assert.Equal("Engineer", work.Title);
        Assert.Equal([profile.WorkExperiences[0].DescriptionText!], work.Bullets); // original text, not "Led a team of 50"
    }

    [Fact]
    public async Task GenerateAsync_falls_back_when_model_drops_an_experience()
    {
        // Source profile has one job; model returned zero — count mismatch must also trigger the guard.
        const string reply =
            """
            {"resumeSummary": "Tailored summary.", "workExperiences": [],
             "skillsOrdered": ["C#"], "coverLetterText": "Dear team,\n\nI would love to join.\n\nBest, Jane"}
            """;
        var generator = new FoundryApplicationDocumentGenerator(new FakeChatClient(reply));
        var profile = MakeProfile();

        var result = await generator.GenerateAsync(profile, MakeJobPosting(), CancellationToken.None);

        var work = Assert.Single(result.WorkExperiences);
        Assert.Equal("Acme", work.Company);
    }

    [Fact]
    public async Task GenerateAsync_drops_skills_not_in_the_source_profile()
    {
        const string reply =
            """
            {"resumeSummary": "Tailored summary.",
             "workExperiences": [{"company": "Acme", "title": "Engineer", "bullets": ["Bullet."]}],
             "skillsOrdered": ["C#", "Rust"], "coverLetterText": "Body."}
            """;
        var generator = new FoundryApplicationDocumentGenerator(new FakeChatClient(reply));

        var result = await generator.GenerateAsync(MakeProfile(), MakeJobPosting(), CancellationToken.None);

        Assert.Equal(["C#"], result.SkillsOrdered); // "Rust" was never in the candidate's real skill list
    }

    [Fact]
    public async Task GenerateAsync_falls_back_to_full_skill_list_when_none_of_the_models_skills_are_real()
    {
        const string reply =
            """
            {"resumeSummary": "Tailored summary.",
             "workExperiences": [{"company": "Acme", "title": "Engineer", "bullets": ["Bullet."]}],
             "skillsOrdered": ["Rust"], "coverLetterText": "Body."}
            """;
        var generator = new FoundryApplicationDocumentGenerator(new FakeChatClient(reply));

        var result = await generator.GenerateAsync(MakeProfile(), MakeJobPosting(), CancellationToken.None);

        Assert.Equal(["C#"], result.SkillsOrdered); // fell back to the source list rather than shipping an empty skills section
    }
}
