using System.Text.Json;
using System.Text.Json.Serialization;
using JobApplyAi.Domain.Abstractions;
using JobApplyAi.Domain.Entities;
using JobApplyAi.Infrastructure.JobSources;
using Microsoft.Extensions.AI;

namespace JobApplyAi.Infrastructure.Ai;

public class FoundryApplicationDocumentGenerator(IChatClient chatClient) : IApplicationDocumentGenerator
{
    private const string SystemPrompt =
        """
        You tailor a candidate's resume and write a cover letter for one specific job posting.

        CRITICAL — the resume must stay factually true to the candidate's real history:
        - workExperiences in your response MUST be exactly the same set of company+title pairs
          given in the candidate's profile, same order, same count. Never add, remove, merge, or
          rename an employer or title.
        - Only the "bullets" under each job may be rewritten: rephrase and reorder to emphasize
          what's relevant to this posting, using ONLY facts, tools, and achievements already
          present in that job's original description. Never invent a tool, metric, or achievement
          that isn't grounded in the original text.
        - skillsOrdered must be a REORDERING/SUBSET of the candidate's given skills (job-relevant
          first) — never add a skill not in the input list.
        - resumeSummary: 2-4 sentences, may synthesize/emphasize but must stay grounded in the
          candidate's actual background — no fabricated claims.

        The cover letter is different: write it fresh, addressed to the hiring team, referencing
        genuine specifics from the candidate's background and the job posting, professional tone,
        3-4 paragraphs, no placeholder brackets like "[Company Name]" — use the real company name.

        Respond with ONLY a JSON object, no markdown fences, matching:
        {"resumeSummary": string,
         "workExperiences": [{"company": string, "title": string, "bullets": [string]}],
         "skillsOrdered": [string],
         "coverLetterText": string}
        """;

    public async Task<TailoredDocuments> GenerateAsync(CandidateProfile profile, JobPosting jobPosting, CancellationToken ct)
    {
        var userMessage = BuildUserMessage(profile, jobPosting);

        var response = await chatClient.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, SystemPrompt),
                new ChatMessage(ChatRole.User, userMessage),
            ],
            new ChatOptions { ResponseFormat = ChatResponseFormat.Json, Temperature = 0.3f },
            ct);

        var json = ChatJsonHelper.StripMarkdownFences(response.Text);
        var result = JsonSerializer.Deserialize<GenerationResult>(json, JsonDefaults.Options)
            ?? throw new InvalidOperationException("Document generation returned empty JSON.");

        // Guard the "same companies, same order, same count" contract at the boundary — if the
        // model drifted from the source profile, fall back to the untailored originals for the
        // work-experience bullets rather than silently shipping a resume with invented history.
        var sourceExperiences = profile.WorkExperiences.ToList();
        var tailoredExperiences = result.WorkExperiences.Count == sourceExperiences.Count
            && result.WorkExperiences.Select(w => (w.Company, w.Title))
                .SequenceEqual(sourceExperiences.Select(w => (w.Company, w.Title)))
            ? result.WorkExperiences.Select(w => new TailoredWorkExperience(w.Company, w.Title, w.Bullets)).ToList()
            : sourceExperiences.Select(w => new TailoredWorkExperience(
                w.Company, w.Title, SplitBullets(w.DescriptionText))).ToList();

        var sourceSkillNames = profile.Skills.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var skillsOrdered = result.SkillsOrdered.Where(s => sourceSkillNames.Contains(s)).ToList();
        if (skillsOrdered.Count == 0)
        {
            skillsOrdered = profile.Skills.Select(s => s.Name).ToList();
        }

        return new TailoredDocuments(result.ResumeSummary, tailoredExperiences, skillsOrdered, result.CoverLetterText);
    }

    private static string BuildUserMessage(CandidateProfile profile, JobPosting jobPosting)
    {
        var experiences = profile.WorkExperiences.Select(w =>
            $"- {w.Title} at {w.Company} ({w.StartDate:yyyy-MM} to {(w.IsCurrent ? "present" : w.EndDate?.ToString("yyyy-MM"))}): {w.DescriptionText}");
        var skills = string.Join(", ", profile.Skills.Select(s => s.Name));

        return $"""
            Candidate profile:
            Name: {profile.FullName}
            Summary: {profile.SummaryText}

            Work experience:
            {string.Join("\n", experiences)}

            Skills: {skills}

            ---

            Job posting:
            {jobPosting.Title} at {jobPosting.CompanyName}
            {jobPosting.DescriptionText}
            """;
    }

    private static List<string> SplitBullets(string? descriptionText)
        => string.IsNullOrWhiteSpace(descriptionText) ? [] : [descriptionText];

    private sealed record GenerationResult(
        [property: JsonPropertyName("resumeSummary")] string ResumeSummary,
        [property: JsonPropertyName("workExperiences")] List<GenerationWorkExperience> WorkExperiences,
        [property: JsonPropertyName("skillsOrdered")] List<string> SkillsOrdered,
        [property: JsonPropertyName("coverLetterText")] string CoverLetterText);

    private sealed record GenerationWorkExperience(
        [property: JsonPropertyName("company")] string Company,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("bullets")] List<string> Bullets);
}
