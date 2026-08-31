namespace JobApplyAi.Domain.Entities;

/// <summary>
/// One row per resume upload/version. Exactly one profile per user is Active and drives matching.
/// The embedding column is a shadow property configured in Infrastructure — the vector type is a
/// provider-specific concern that must not leak into Domain.
/// </summary>
public class CandidateProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public ProfileStatus Status { get; set; }

    public string? RawResumeBlobUrl { get; set; }
    public string? RawResumeFileName { get; set; }
    public string? RawResumeContentType { get; set; }

    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? LocationText { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? PortfolioUrl { get; set; }
    public string? SummaryText { get; set; }

    /// <summary>Hard matching requirements, not soft preferences — see MatchingPipelineService.</summary>
    public bool RequiresVisaSponsorship { get; set; }

    /// <summary>Null = no floor set. Excludes a posting only when its stated Max is confidently below this.</summary>
    public int? MinimumSalaryUsd { get; set; }

    /// <summary>ISO 3166-1 alpha-2 (e.g. "US"). Null = no restriction. Excludes a posting only when
    /// it confidently states a DIFFERENT single country — ambiguous/global postings still pass.</summary>
    public string? RequiredCountry { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ParsedAtUtc { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }

    public List<ProfileWorkExperience> WorkExperiences { get; set; } = [];
    public List<ProfileEducation> Educations { get; set; } = [];
    public List<ProfileSkill> Skills { get; set; } = [];
    public List<ProfileExcludedCompany> ExcludedCompanies { get; set; } = [];
}
