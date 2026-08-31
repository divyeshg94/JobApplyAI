using System.Text.Json;
using System.Text.Json.Serialization;
using JobApplyAi.Domain;
using JobApplyAi.Domain.Abstractions;
using JobApplyAi.Domain.Entities;

namespace JobApplyAi.Infrastructure.JobSources;

/// <summary>
/// Official public job-board API: GET /v1/boards/{boardToken}/jobs?content=true.
/// Keyless, returns the full list in one call — no pagination.
/// </summary>
public class GreenhouseJobSourceClient(HttpClient httpClient) : IJobSourceClient
{
    public const string BaseUrl = "https://boards-api.greenhouse.io/";

    public JobSource Source => JobSource.Greenhouse;

    public async Task<JobFetchResult> FetchJobsAsync(
        JobSourceSubscription subscription, JobFetchCursor? cursor, CancellationToken ct)
    {
        var config = JsonSerializer.Deserialize<GreenhouseSubscriptionConfig>(
            subscription.ConfigJson, JsonDefaults.Options)
            ?? throw new InvalidOperationException(
                $"Subscription {subscription.Id} has invalid Greenhouse config.");

        var url = $"v1/boards/{Uri.EscapeDataString(config.BoardToken)}/jobs?content=true";
        using var response = await httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        var parsed = JsonSerializer.Deserialize<GreenhouseJobsResponse>(body, JsonDefaults.Options)
            ?? new GreenhouseJobsResponse([]);

        var jobs = parsed.Jobs.Select(j => new RawJobPosting(
                ExternalJobId: j.Id.ToString(),
                Title: j.Title,
                CompanyName: subscription.DisplayName,
                LocationText: j.Location?.Name,
                DescriptionText: HtmlText.ToPlainText(j.Content ?? string.Empty),
                ApplyUrl: j.AbsoluteUrl,
                PostedAtUtc: ParseDate(j.FirstPublished) ?? ParseDate(j.UpdatedAt),
                RawJson: JsonSerializer.Serialize(j, JsonDefaults.Options)))
            .ToList();

        return new JobFetchResult(jobs, NextCursor: null, HasMore: false);
    }

    private static DateTimeOffset? ParseDate(string? value)
        => DateTimeOffset.TryParse(value, out var result) ? result : null;

    private sealed record GreenhouseJobsResponse(
        [property: JsonPropertyName("jobs")] IReadOnlyList<GreenhouseJob> Jobs);

    private sealed record GreenhouseJob(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("absolute_url")] string AbsoluteUrl,
        [property: JsonPropertyName("location")] GreenhouseLocation? Location,
        [property: JsonPropertyName("content")] string? Content,
        [property: JsonPropertyName("first_published")] string? FirstPublished,
        [property: JsonPropertyName("updated_at")] string? UpdatedAt);

    private sealed record GreenhouseLocation(
        [property: JsonPropertyName("name")] string? Name);
}
