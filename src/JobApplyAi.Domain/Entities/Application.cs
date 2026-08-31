namespace JobApplyAi.Domain.Entities;

public class Application
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid MatchResultId { get; set; }
    public MatchResult? MatchResult { get; set; }

    public ApplicationStatus Status { get; set; }

    public string? TailoredResumeBlobUrl { get; set; }
    public string? TailoredCoverLetterBlobUrl { get; set; }

    public DateTimeOffset? GeneratedAtUtc { get; set; }
    public DateTimeOffset? AppliedAtUtc { get; set; }
    public string? Notes { get; set; }
}
