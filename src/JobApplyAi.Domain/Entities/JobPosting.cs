namespace JobApplyAi.Domain.Entities;

/// <summary>
/// Global (not user-scoped) job pool — matches are per-user, postings are shared.
/// Dedup key: unique (Source, ExternalJobId). The embedding column is a shadow property
/// configured in Infrastructure.
/// </summary>
public class JobPosting
{
    public Guid Id { get; set; }
    public JobSource Source { get; set; }
    public required string ExternalJobId { get; set; }

    public required string Title { get; set; }
    public required string CompanyName { get; set; }
    public string? LocationText { get; set; }
    public string? DescriptionText { get; set; }
    public required string ApplyUrl { get; set; }

    public DateTimeOffset? PostedAtUtc { get; set; }
    public DateTimeOffset FetchedAtUtc { get; set; }
    public required string RawJsonPayload { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Null until classification has run once (the gate MatchingPipelineService uses to decide
    /// what needs (re)classifying) — deliberately separate from the nullable fields below, since
    /// null is also their valid FINAL state (e.g. "no deadline stated"). Using one of them as the
    /// gate would mean a posting classified before a new field existed never gets backfilled.
    /// </summary>
    public DateTimeOffset? ClassifiedAtUtc { get; set; }

    /// <summary>All fields below are extracted together in one LLM pass — see FoundryJobPostingClassifier.</summary>
    public VisaSponsorshipStatus? VisaSponsorship { get; set; }

    /// <summary>
    /// Annualized USD, extracted from description text when stated (hourly normalized to annual).
    /// Null means "not stated or not confidently extractable" — never treated as failing a salary
    /// floor, only an explicit sub-floor Max does. Non-USD currencies are left null rather than
    /// guessed via FX conversion.
    /// </summary>
    public int? SalaryMinAnnualUsd { get; set; }
    public int? SalaryMaxAnnualUsd { get; set; }

    /// <summary>Null = no stated deadline (most postings) or not yet classified — never excluded on that basis.</summary>
    public DateOnly? ApplicationDeadline { get; set; }

    /// <summary>
    /// ISO 3166-1 alpha-2 (e.g. "US"), when the posting confidently restricts to one country.
    /// Null = open/global, ambiguous, or not yet classified — never excluded on that basis; only
    /// an explicit, different country excludes (see MatchingPipelineService).
    /// </summary>
    public string? WorkLocationCountry { get; set; }
}
