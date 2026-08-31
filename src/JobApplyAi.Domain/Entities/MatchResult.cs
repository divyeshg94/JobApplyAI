namespace JobApplyAi.Domain.Entities;

public class MatchResult
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid JobPostingId { get; set; }
    public JobPosting? JobPosting { get; set; }
    public Guid CandidateProfileId { get; set; }
    public CandidateProfile? CandidateProfile { get; set; }

    public double VectorScore { get; set; }
    public double LlmScore { get; set; }
    public string? LlmReasoning { get; set; }

    public MatchStatus Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? NotifiedAtUtc { get; set; }
}
