using JobApplyAi.Domain;

namespace JobApplyAi.Api.Options;

public class PollingOptions
{
    public const string SectionName = "Polling";

    /// <summary>Base tick — each tick only fetches subscriptions whose per-source interval is due.</summary>
    public int BaseTickSeconds { get; set; } = 300;

    /// <summary>Safety cap on pagination loops (Adzuna).</summary>
    public int PageCap { get; set; } = 10;

    /// <summary>
    /// Per-source cadence. Adzuna's free tier is quota-limited (~250 calls/day) so it polls a few
    /// times a day; Greenhouse/Lever are keyless and cheap.
    /// </summary>
    public Dictionary<string, int> SourceIntervalMinutes { get; set; } = new()
    {
        [nameof(JobSource.Greenhouse)] = 60,
        [nameof(JobSource.Lever)] = 60,
        [nameof(JobSource.Adzuna)] = 360,
    };

    public TimeSpan IntervalFor(JobSource source)
        => TimeSpan.FromMinutes(
            SourceIntervalMinutes.TryGetValue(source.ToString(), out var minutes) ? minutes : 60);
}
