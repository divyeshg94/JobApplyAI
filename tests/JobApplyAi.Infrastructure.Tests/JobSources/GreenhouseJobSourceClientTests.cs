using JobApplyAi.Domain;
using JobApplyAi.Domain.Entities;
using JobApplyAi.Domain.Seed;
using JobApplyAi.Infrastructure.JobSources;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace JobApplyAi.Infrastructure.Tests.JobSources;

public class GreenhouseJobSourceClientTests : IDisposable
{
    private readonly WireMockServer _server = WireMockServer.Start();

    public void Dispose() => _server.Stop();

    private GreenhouseJobSourceClient CreateClient()
        => new(new HttpClient { BaseAddress = new Uri(_server.Urls[0]) });

    private static JobSourceSubscription Subscription(string configJson) => new()
    {
        Id = Guid.NewGuid(),
        UserId = SeedData.DefaultUserId,
        Source = JobSource.Greenhouse,
        DisplayName = "Acme Corp",
        ConfigJson = configJson,
    };

    [Fact]
    public async Task FetchJobs_maps_fields_and_strips_html()
    {
        _server.Given(Request.Create()
                .WithPath("/v1/boards/acme/jobs")
                .WithParam("content", "true")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new
                {
                    jobs = new[]
                    {
                        new
                        {
                            id = 12345L,
                            title = "Senior Engineer",
                            absolute_url = "https://boards.greenhouse.io/acme/jobs/12345",
                            location = new { name = "Remote - EU" },
                            content = "&lt;p&gt;Build &amp;amp; ship things&lt;/p&gt;",
                            first_published = "2026-07-01T10:00:00-04:00",
                            updated_at = "2026-07-10T10:00:00-04:00",
                        },
                    },
                }));

        var result = await CreateClient().FetchJobsAsync(
            Subscription("""{"boardToken":"acme"}"""), null, CancellationToken.None);

        Assert.False(result.HasMore);
        Assert.Null(result.NextCursor);
        var job = Assert.Single(result.Jobs);
        Assert.Equal("12345", job.ExternalJobId);
        Assert.Equal("Senior Engineer", job.Title);
        Assert.Equal("Acme Corp", job.CompanyName);
        Assert.Equal("Remote - EU", job.LocationText);
        Assert.Equal("Build & ship things", job.DescriptionText);
        Assert.Equal("https://boards.greenhouse.io/acme/jobs/12345", job.ApplyUrl);
        Assert.Equal(new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.FromHours(-4)), job.PostedAtUtc);
    }

    [Fact]
    public async Task FetchJobs_throws_on_http_error()
    {
        _server.Given(Request.Create().WithPath("/v1/boards/gone/jobs").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));

        await Assert.ThrowsAsync<HttpRequestException>(() => CreateClient().FetchJobsAsync(
            Subscription("""{"boardToken":"gone"}"""), null, CancellationToken.None));
    }
}
