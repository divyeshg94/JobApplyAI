namespace JobApplyAi.Domain.Entities;

/// <summary>
/// What the poller watches. ConfigJson shape differs per source:
/// Greenhouse {"boardToken":"acme"}, Lever {"company":"acme"},
/// Adzuna {"keywords":"...","location":"...","country":"us"}.
/// </summary>
public class JobSourceSubscription
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public JobSource Source { get; set; }
    public required string ConfigJson { get; set; }
    public required string DisplayName { get; set; }
    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset? LastPolledAtUtc { get; set; }
    public PollStatus? LastPollStatus { get; set; }
    public string? LastPollError { get; set; }
}
