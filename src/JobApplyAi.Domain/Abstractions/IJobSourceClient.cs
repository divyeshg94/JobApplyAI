using JobApplyAi.Domain.Entities;

namespace JobApplyAi.Domain.Abstractions;

/// <summary>
/// One implementation per <see cref="JobSource"/>. Per-source differences (auth, pagination,
/// response shape) stay fully inside the adapter; adding a source never touches the polling loop.
/// </summary>
public interface IJobSourceClient
{
    JobSource Source { get; }

    Task<JobFetchResult> FetchJobsAsync(
        JobSourceSubscription subscription,
        JobFetchCursor? cursor,
        CancellationToken ct);
}

public sealed record JobFetchResult(
    IReadOnlyList<RawJobPosting> Jobs,
    JobFetchCursor? NextCursor,
    bool HasMore);

public sealed record RawJobPosting(
    string ExternalJobId,
    string Title,
    string CompanyName,
    string? LocationText,
    string DescriptionText,
    string ApplyUrl,
    DateTimeOffset? PostedAtUtc,
    string RawJson);

/// <summary>Meaning is source-specific: Adzuna uses <see cref="Page"/>; Greenhouse/Lever return everything in one call.</summary>
public sealed record JobFetchCursor(int? Page, string? OpaqueToken);
