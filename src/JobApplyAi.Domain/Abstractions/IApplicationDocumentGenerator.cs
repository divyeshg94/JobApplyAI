using JobApplyAi.Domain.Entities;

namespace JobApplyAi.Domain.Abstractions;

/// <summary>
/// Tailors a resume + cover letter to one job posting. The resume tailoring is REPHRASE/REORDER
/// only — same companies, titles, and dates as the source profile, never invented ones (resume
/// fraud risk). The cover letter is net-new generated text, which is normal for a cover letter.
/// </summary>
public interface IApplicationDocumentGenerator
{
    Task<TailoredDocuments> GenerateAsync(CandidateProfile profile, JobPosting jobPosting, CancellationToken ct);
}

public sealed record TailoredDocuments(
    string ResumeSummary,
    IReadOnlyList<TailoredWorkExperience> WorkExperiences,
    IReadOnlyList<string> SkillsOrdered,
    string CoverLetterText);

/// <summary>Company/Title must match a real ProfileWorkExperience — only Bullets are LLM-authored.</summary>
public sealed record TailoredWorkExperience(string Company, string Title, IReadOnlyList<string> Bullets);
