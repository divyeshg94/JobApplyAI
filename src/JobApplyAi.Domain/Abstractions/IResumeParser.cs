namespace JobApplyAi.Domain.Abstractions;

/// <summary>
/// Extracts a structured profile from an uploaded resume. Output always goes through the
/// human review/edit screen before becoming Active — never trusted blindly.
/// </summary>
public interface IResumeParser
{
    Task<ParsedResume> ParseAsync(byte[] content, string fileName, string contentType, CancellationToken ct);
}

public sealed record ParsedResume(
    string? FullName,
    string? Email,
    string? Phone,
    string? LocationText,
    string? LinkedInUrl,
    string? PortfolioUrl,
    string? SummaryText,
    IReadOnlyList<ParsedWorkExperience> WorkExperiences,
    IReadOnlyList<ParsedEducation> Educations,
    IReadOnlyList<ParsedSkill> Skills);

public sealed record ParsedWorkExperience(
    string Company,
    string Title,
    string? LocationText,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool IsCurrent,
    string? DescriptionText);

public sealed record ParsedEducation(
    string Institution,
    string? Degree,
    string? FieldOfStudy,
    DateOnly? StartDate,
    DateOnly? EndDate);

public sealed record ParsedSkill(string Name, string? Category);
