using System.Text.Json;
using System.Text.Json.Serialization;
using JobApplyAi.Domain;
using JobApplyAi.Domain.Abstractions;
using JobApplyAi.Domain.Entities;
using Microsoft.Extensions.Options;

namespace JobApplyAi.Infrastructure.JobSources;

/// <summary>
/// Aggregator search API: GET /v1/api/jobs/{country}/search/{page}. Paginated, and the only
/// source needing credentials (free tier is quota-limited — poll sparingly, see PollingOptions).
/// </summary>
public class AdzunaJobSourceClient(HttpClient httpClient, IOptions<AdzunaOptions> options) : IJobSourceClient
{
    public const string BaseUrl = "https://api.adzuna.com/";
    private const int ResultsPerPage = 50;

    public JobSource Source => JobSource.Adzuna;

    public async Task<JobFetchResult> FetchJobsAsync(
        JobSourceSubscription subscription, JobFetchCursor? cursor, CancellationToken ct)
    {
        var config = JsonSerializer.Deserialize<AdzunaSubscriptionConfig>(
            subscription.ConfigJson, JsonDefaults.Options)
            ?? throw new InvalidOperationException(
                $"Subscription {subscription.Id} has invalid Adzuna config.");

        var credentials = options.Value;
        if (string.IsNullOrEmpty(credentials.AppId) || string.IsNullOrEmpty(credentials.AppKey))
        {
            throw new InvalidOperationException(
                "Adzuna:AppId / Adzuna:AppKey not configured (user-secrets or App Service settings).");
        }

        var page = cursor?.Page ?? 1;
        var url = $"v1/api/jobs/{Uri.EscapeDataString(config.Country)}/search/{page}" +
                  $"?app_id={Uri.EscapeDataString(credentials.AppId)}" +
                  $"&app_key={Uri.EscapeDataString(credentials.AppKey)}" +
                  $"&what={Uri.EscapeDataString(config.Keywords)}" +
                  $"&results_per_page={ResultsPerPage}" +
                  "&content-type=application/json";
        if (!string.IsNullOrWhiteSpace(config.Location))
        {
            url += $"&where={Uri.EscapeDataString(config.Location)}";
        }

        using var response = await httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        var parsed = JsonSerializer.Deserialize<AdzunaSearchResponse>(body, JsonDefaults.Options)
            ?? new AdzunaSearchResponse(0, []);

        var jobs = parsed.Results.Select(r => new RawJobPosting(
                ExternalJobId: r.Id,
                Title: r.Title,
                CompanyName: r.Company?.DisplayName ?? "Unknown",
                LocationText: r.Location?.DisplayName,
                DescriptionText: r.Description ?? string.Empty,
                ApplyUrl: r.RedirectUrl,
                PostedAtUtc: DateTimeOffset.TryParse(r.Created, out var created) ? created : null,
                RawJson: JsonSerializer.Serialize(r, JsonDefaults.Options)))
            .ToList();

        var hasMore = page * ResultsPerPage < parsed.Count && parsed.Results.Count > 0;
        return new JobFetchResult(
            jobs,
            NextCursor: hasMore ? new JobFetchCursor(page + 1, null) : null,
            HasMore: hasMore);
    }

    private sealed record AdzunaSearchResponse(
        [property: JsonPropertyName("count")] int Count,
        [property: JsonPropertyName("results")] IReadOnlyList<AdzunaResult> Results);

    private sealed record AdzunaResult(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("redirect_url")] string RedirectUrl,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("created")] string? Created,
        [property: JsonPropertyName("company")] AdzunaCompany? Company,
        [property: JsonPropertyName("location")] AdzunaLocation? Location);

    private sealed record AdzunaCompany(
        [property: JsonPropertyName("display_name")] string? DisplayName);

    private sealed record AdzunaLocation(
        [property: JsonPropertyName("display_name")] string? DisplayName);
}

public class AdzunaOptions
{
    public const string SectionName = "Adzuna";

    public string? AppId { get; set; }
    public string? AppKey { get; set; }
}
