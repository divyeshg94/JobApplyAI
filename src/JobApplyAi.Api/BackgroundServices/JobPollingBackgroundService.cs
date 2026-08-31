using JobApplyAi.Api.Options;
using JobApplyAi.Api.Services;
using JobApplyAi.Domain;
using JobApplyAi.Domain.Abstractions;
using JobApplyAi.Domain.Entities;
using JobApplyAi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobApplyAi.Api.BackgroundServices;

/// <summary>
/// Milestone-3 scope: fetch → dedupe → persist → log, per-source cadence, per-subscription fault
/// isolation. Embedding/prefilter/rescore/notify (milestone 4/5) extend this after persistence.
/// Requires App Service Always On — the timer dies with the idle process otherwise.
/// </summary>
public class JobPollingBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<PollingOptions> options,
    ILogger<JobPollingBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.BaseTickSeconds));
        do
        {
            try
            {
                await RunTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Polling tick failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunTickAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clients = scope.ServiceProvider.GetRequiredService<IEnumerable<IJobSourceClient>>()
            .ToDictionary(c => c.Source);

        var now = DateTimeOffset.UtcNow;
        var subscriptions = await db.JobSourceSubscriptions
            .Where(s => s.IsEnabled)
            .ToListAsync(ct);
        var due = subscriptions
            .Where(s => s.LastPolledAtUtc is null
                || now - s.LastPolledAtUtc >= options.Value.IntervalFor(s.Source))
            .ToList();

        foreach (var subscription in due)
        {
            // Fault isolation: one bad source/subscription never blocks the others.
            var log = new PollRunLog
            {
                Id = Guid.NewGuid(),
                JobSourceSubscriptionId = subscription.Id,
                StartedAtUtc = DateTimeOffset.UtcNow,
            };
            try
            {
                var (fetched, added) = await PollSubscriptionAsync(db, clients[subscription.Source], subscription, ct);
                log.JobsFetched = fetched;
                log.JobsNew = added;
                subscription.LastPollStatus = PollStatus.Ok;
                subscription.LastPollError = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Polling {Source} subscription {Name} failed.",
                    subscription.Source, subscription.DisplayName);
                log.JobsFailed = 1;
                log.ErrorMessage = Truncate(ex.Message, 2000);
                subscription.LastPollStatus = PollStatus.Error;
                subscription.LastPollError = Truncate(ex.Message, 2000);
            }

            subscription.LastPolledAtUtc = DateTimeOffset.UtcNow;
            log.CompletedAtUtc = DateTimeOffset.UtcNow;
            db.PollRunLogs.Add(log);
            await db.SaveChangesAsync(ct);
        }

        try
        {
            await scope.ServiceProvider.GetRequiredService<MatchingPipelineService>().RunAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A bad matching tick (e.g. Foundry outage) must not affect the next poll — it just
            // retries on the following tick since no MatchResult rows were created.
            logger.LogError(ex, "Matching pipeline failed.");
        }

        try
        {
            await scope.ServiceProvider.GetRequiredService<NotificationService>().RunAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Same isolation as matching — a failed send leaves matches PendingReview, retried
            // (and re-batched with newer ones) next tick.
            logger.LogError(ex, "Notification pipeline failed.");
        }
    }

    private async Task<(int Fetched, int Added)> PollSubscriptionAsync(
        AppDbContext db, IJobSourceClient client, JobSourceSubscription subscription, CancellationToken ct)
    {
        var fetched = new List<RawJobPosting>();
        JobFetchCursor? cursor = null;
        for (var page = 0; page < options.Value.PageCap; page++)
        {
            var result = await client.FetchJobsAsync(subscription, cursor, ct);
            fetched.AddRange(result.Jobs);
            if (!result.HasMore || result.NextCursor is null)
            {
                break;
            }

            cursor = result.NextCursor;
        }

        var externalIds = fetched.Select(j => j.ExternalJobId).Distinct().ToList();
        var existing = await db.JobPostings
            .Where(p => p.Source == client.Source && externalIds.Contains(p.ExternalJobId))
            .Select(p => p.ExternalJobId)
            .ToListAsync(ct);
        var seen = new HashSet<string>(existing);

        var added = 0;
        foreach (var job in fetched)
        {
            // HashSet also dedupes repeats within this batch (unique index is the backstop).
            if (!seen.Add(job.ExternalJobId))
            {
                continue;
            }

            db.JobPostings.Add(new JobPosting
            {
                Id = Guid.NewGuid(),
                Source = client.Source,
                ExternalJobId = job.ExternalJobId,
                Title = Truncate(job.Title, 300),
                CompanyName = Truncate(job.CompanyName, 200),
                LocationText = job.LocationText is null ? null : Truncate(job.LocationText, 300),
                DescriptionText = job.DescriptionText,
                ApplyUrl = Truncate(job.ApplyUrl, 1000),
                PostedAtUtc = job.PostedAtUtc,
                FetchedAtUtc = DateTimeOffset.UtcNow,
                RawJsonPayload = job.RawJson,
                IsActive = true,
            });
            added++;
        }

        return (fetched.Count, added);
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];
}
