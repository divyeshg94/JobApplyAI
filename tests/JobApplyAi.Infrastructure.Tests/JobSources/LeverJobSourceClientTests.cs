using JobApplyAi.Domain;
using JobApplyAi.Domain.Entities;
using JobApplyAi.Domain.Seed;
using JobApplyAi.Infrastructure.JobSources;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace JobApplyAi.Infrastructure.Tests.JobSources;

public class LeverJobSourceClientTests : IDisposable
{
    private readonly WireMockServer _server = WireMockServer.Start();

    public void Dispose() => _server.Stop();

    [Fact]
    public async Task FetchJobs_maps_fields_and_joins_description_parts()
    {
        _server.Given(Request.Create()
                .WithPath("/v0/postings/acme")
                .WithParam("mode", "json")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new object[]
                {
                    new
                    {
                        id = "abc-123",
                        text = "Backend Engineer",
                        hostedUrl = "https://jobs.lever.co/acme/abc-123",
                        categories = new { location = "Amsterdam" },
                        descriptionPlain = "Main description.",
                        additionalPlain = "Extra notes.",
                        createdAt = 1751364000000L,
                    },
                }));

        var client = new LeverJobSourceClient(new HttpClient { BaseAddress = new Uri(_server.Urls[0]) });
        var subscription = new JobSourceSubscription
        {
            Id = Guid.NewGuid(),
            UserId = SeedData.DefaultUserId,
            Source = JobSource.Lever,
            DisplayName = "Acme Corp",
            ConfigJson = """{"company":"acme"}""",
        };

        var result = await client.FetchJobsAsync(subscription, null, CancellationToken.None);

        Assert.False(result.HasMore);
        var job = Assert.Single(result.Jobs);
        Assert.Equal("abc-123", job.ExternalJobId);
        Assert.Equal("Backend Engineer", job.Title);
        Assert.Equal("Acme Corp", job.CompanyName);
        Assert.Equal("Amsterdam", job.LocationText);
        Assert.Equal("Main description.\n\nExtra notes.", job.DescriptionText);
        Assert.Equal("https://jobs.lever.co/acme/abc-123", job.ApplyUrl);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1751364000000L), job.PostedAtUtc);
    }
}
