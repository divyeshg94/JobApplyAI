namespace JobApplyAi.Domain;

public enum ProfileStatus
{
    Parsing = 0,
    NeedsReview = 1,
    Active = 2,
    Superseded = 3,
    Failed = 4,
}

public enum JobSource
{
    Greenhouse = 0,
    Lever = 1,
    Adzuna = 2,
}

public enum PollStatus
{
    Ok = 0,
    Error = 1,
}

public enum MatchStatus
{
    PendingReview = 0,
    Notified = 1,
    Dismissed = 2,
}

public enum ApplicationStatus
{
    Matched = 0,
    Prepped = 1,
    Applied = 2,
    Withdrawn = 3,
}

/// <summary>
/// Classified once per JobPosting from its description text. Unspecified (posting says nothing
/// either way) is deliberately NOT treated as disqualifying — most companies that do sponsor
/// never mention it explicitly, so only an explicit NoSponsorship excludes a posting.
/// </summary>
public enum VisaSponsorshipStatus
{
    Unspecified = 0,
    Sponsors = 1,
    NoSponsorship = 2,
}
