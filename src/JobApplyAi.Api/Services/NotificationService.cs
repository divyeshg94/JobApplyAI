using System.Net;
using System.Text;
using JobApplyAi.Api.Options;
using JobApplyAi.Domain;
using JobApplyAi.Domain.Abstractions;
using JobApplyAi.Domain.Entities;
using JobApplyAi.Domain.Seed;
using JobApplyAi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobApplyAi.Api.Services;

/// <summary>
/// Milestone 5, docs/architecture.md §5 step 8. Batches all qualifying matches into ONE digest
/// email per tick rather than one email per match — with matching potentially surfacing dozens of
/// results in a single tick (e.g. the first run against a large existing posting pool), per-match
/// emails would read as spam. No separate notification table: MatchResults.Status doubles as the
/// in-app feed, queried directly by the dashboard.
/// </summary>
public class NotificationService(
    AppDbContext db,
    IEmailNotifier emailNotifier,
    IOptions<MatchingOptions> options,
    ILogger<NotificationService> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == SeedData.DefaultUserId, ct);
        if (user is null || string.IsNullOrWhiteSpace(user.Email) || user.Email == SeedData.PlaceholderEmail)
        {
            logger.LogWarning(
                "jobapply.Users.Email is still the seed placeholder — set a real address to receive match notifications.");
            return;
        }

        var qualifying = await db.MatchResults
            .Where(m => m.UserId == SeedData.DefaultUserId
                && m.Status == MatchStatus.PendingReview
                && m.LlmScore >= options.Value.NotifyThreshold)
            .Join(db.JobPostings, m => m.JobPostingId, j => j.Id, (m, j) => new { Match = m, Posting = j })
            .OrderByDescending(x => x.Match.LlmScore)
            .ToListAsync(ct);

        if (qualifying.Count == 0)
        {
            return;
        }

        var bodyHtml = BuildDigestHtml(qualifying.Select(x => (x.Match, x.Posting)));
        var subject = $"JobApplyAi: {qualifying.Count} new match{(qualifying.Count == 1 ? "" : "es")}";

        try
        {
            await emailNotifier.SendAsync(user.Email, subject, bodyHtml, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Don't mark Notified on a failed send — these stay PendingReview and get retried
            // (and re-batched with any newer matches) next tick.
            logger.LogError(ex, "Sending match digest email failed.");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var item in qualifying)
        {
            item.Match.Status = MatchStatus.Notified;
            item.Match.NotifiedAtUtc = now;
        }
        await db.SaveChangesAsync(ct);
    }

    private static string BuildDigestHtml(IEnumerable<(MatchResult Match, JobPosting Posting)> items)
    {
        var builder = new StringBuilder();
        builder.Append("<h2>New job matches</h2><table border=\"1\" cellpadding=\"6\" cellspacing=\"0\">");
        builder.Append("<tr><th>Score</th><th>Title</th><th>Company</th><th>Reasoning</th><th>Apply</th></tr>");

        foreach (var (match, posting) in items)
        {
            builder.Append("<tr>");
            builder.Append($"<td>{match.LlmScore:0}</td>");
            builder.Append($"<td>{WebUtility.HtmlEncode(posting.Title)}</td>");
            builder.Append($"<td>{WebUtility.HtmlEncode(posting.CompanyName)}</td>");
            builder.Append($"<td>{WebUtility.HtmlEncode(match.LlmReasoning)}</td>");
            builder.Append($"<td><a href=\"{WebUtility.HtmlEncode(posting.ApplyUrl)}\">Apply</a></td>");
            builder.Append("</tr>");
        }

        builder.Append("</table>");
        return builder.ToString();
    }
}
