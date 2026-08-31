namespace JobApplyAi.Domain.Entities;

public class PollRunLog
{
    public Guid Id { get; set; }
    public Guid JobSourceSubscriptionId { get; set; }
    public JobSourceSubscription? JobSourceSubscription { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public int JobsFetched { get; set; }
    public int JobsNew { get; set; }
    public int JobsFailed { get; set; }
    public string? ErrorMessage { get; set; }
}
