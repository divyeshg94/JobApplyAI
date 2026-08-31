using JobApplyAi.Domain;
using JobApplyAi.Domain.Entities;
using JobApplyAi.Domain.Seed;
using JobApplyAi.Infrastructure.JobSources;
using Microsoft.Extensions.Options;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace JobApplyAi.Infrastructure.Tests.JobSources;

public class AdzunaJobSourceClientTests : IDisposable
{
    private readonly WireMockServer _server = WireMockServer.Start();

    public void Dispose() => _server.Stop();

    private AdzunaJobSourceClient CreateClient(string? appId = "test-id", string? appKey = "test-key")
        => new(
            new HttpClient { BaseAddress = new Uri(_server.Urls[0]) },
            Microsoft.Extensions.Options.Options.Create(new AdzunaOptions { AppId = appId, AppKey = appKey }));

    private static JobSourceSubscription Subscription() => new()
    {
        Id = Guid.NewGuid(),
        UserId = SeedData.DefaultUserId,
        Source = JobSource.Adzuna,
        DisplayName = "NL software search",
        ConfigJson = """{"keywords":"software engineer","location":"amsterdam","country":"nl"}""",
    };

    [Fact]
    public async Task FetchJobs_paginates_when_more_results_exist()
    {
        _server.Given(Request.Create()
                .WithPath("/v1/api/jobs/nl/search/1")
                .WithParam("app_id", "test-id")
                .WithParam("app_key", "test-key")
                .WithParam("what", "software engineer")
                .WithParam("where", "amsterdam")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new
                {
                    count = 120,
                    results = Enumerable.Range(1, 50).Select(i => new
                    {
                        id = $"job-{i}",
                        title = $"Engineer {i}",
                        redirect_url = $"https://adzuna.example/{i}",
                        description = "Do work.",
                        created = "2026-07-05T08:00:00Z",
                        company = new { display_name = "SomeCo" },
                        location = new { display_name = "Amsterdam, NL" },
                    }).ToArray(),
                }));

        var result = await CreateClient().FetchJobsAsync(Subscription(), null, CancellationToken.None);

        Assert.True(result.HasMore);
        Assert.Equal(2, result.NextCursor?.Page);
        Assert.Equal(50, result.Jobs.Count);
        Assert.Equal("job-1", result.Jobs[0].ExternalJobId);
        Assert.Equal("SomeCo", result.Jobs[0].CompanyName);
    }

    [Fact]
    public async Task FetchJobs_last_page_reports_no_more()
    {
        _server.Given(Request.Create().WithPath("/v1/api/jobs/nl/search/3").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new
                {
                    count = 120,
                    results = Enumerable.Range(101, 20).Select(i => new
                    {
                        id = $"job-{i}",
                        title = $"Engineer {i}",
                        redirect_url = $"https://adzuna.example/{i}",
                        description = "Do work.",
                        created = "2026-07-05T08:00:00Z",
                        company = new { display_name = "SomeCo" },
                        location = new { display_name = "Amsterdam, NL" },
                    }).ToArray(),
                }));

        var result = await CreateClient().FetchJobsAsync(
            Subscription(), new JobApplyAi.Domain.Abstractions.JobFetchCursor(3, null), CancellationToken.None);

        Assert.False(result.HasMore);
        Assert.Null(result.NextCursor);
        Assert.Equal(20, result.Jobs.Count);
    }

    [Fact]
    public async Task FetchJobs_throws_when_credentials_missing()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateClient(appId: null)
            .FetchJobsAsync(Subscription(), null, CancellationToken.None));
    }
}
