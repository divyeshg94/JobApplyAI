using System.Text.Json;
using System.Text.Json.Serialization;
using JobApplyAi.Domain;
using JobApplyAi.Domain.Abstractions;
using JobApplyAi.Domain.Entities;

namespace JobApplyAi.Infrastructure.JobSources;

/// <summary>
/// Official public postings API: GET /v0/postings/{company}?mode=json.
/// Keyless, full list in one call — no pagination.
/// </summary>
public class LeverJobSourceClient(HttpClient httpClient) : IJobSourceClient
{
    public const string BaseUrl = "https://api.lever.co/";

    public JobSource Source => JobSource.Lever;

    public async Task<JobFetchResult> FetchJobsAsync(
        JobSourceSubscription subscription, JobFetchCursor? cursor, CancellationToken ct)
    {
        var config = JsonSerializer.Deserialize<LeverSubscriptionConfig>(
            subscription.ConfigJson, JsonDefaults.Options)
            ?? throw new InvalidOperationException(
                $"Subscription {subscription.Id} has invalid Lever config.");

        var url = $"v0/postings/{Uri.EscapeDataString(config.Company)}?mode=json";
        using var response = await httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        var postings = JsonSerializer.Deserialize<IReadOnlyList<LeverPosting>>(body, JsonDefaults.Options)
            ?? [];

        var jobs = postings.Select(p => new RawJobPosting(
                ExternalJobId: p.Id,
                Title: p.Text,
                CompanyName: subscription.DisplayName,
                LocationText: p.Categories?.Location,
                DescriptionText: string.Join("\n\n",
                    new[] { p.DescriptionPlain, p.AdditionalPlain }
                        .Where(s => !string.IsNullOrWhiteSpace(s))),
                ApplyUrl: p.HostedUrl,
                PostedAtUtc: p.CreatedAt is { } ms
                    ? DateTimeOffset.FromUnixTimeMilliseconds(ms)
                    : null,
                RawJson: JsonSerializer.Serialize(p, JsonDefaults.Options)))
            .ToList();

        return new JobFetchResult(jobs, NextCursor: null, HasMore: false);
    }

    private sealed record LeverPosting(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("hostedUrl")] string HostedUrl,
        [property: JsonPropertyName("categories")] LeverCategories? Categories,
        [property: JsonPropertyName("descriptionPlain")] string? DescriptionPlain,
        [property: JsonPropertyName("additionalPlain")] string? AdditionalPlain,
        [property: JsonPropertyName("createdAt")] long? CreatedAt);

    private sealed record LeverCategories(
        [property: JsonPropertyName("location")] string? Location);
}
