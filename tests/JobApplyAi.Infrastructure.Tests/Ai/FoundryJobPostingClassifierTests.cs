using JobApplyAi.Domain;
using JobApplyAi.Domain.Abstractions;
using JobApplyAi.Infrastructure.Ai;

namespace JobApplyAi.Infrastructure.Tests.Ai;

public class FoundryJobPostingClassifierTests
{
    private static List<JobPostingClassificationInput> OnePosting() =>
        [new JobPostingClassificationInput("Engineer", "Acme", "Remote - US", "We sponsor visas. $150,000-$180,000. Apply by 2026-12-01.")];

    [Fact]
    public async Task ClassifyAsync_parses_the_object_wrapped_array_the_v1_json_mode_requires()
    {
        // Regression test: Azure OpenAI's json_object response format requires a top-level
        // object, not a bare array — this classifier previously asked for a bare `[...]` and
        // 400'd on every real call. The fix wraps it in {"classifications": [...]}.
        const string reply =
            """
            {"classifications": [{"visaSponsorship": "Sponsors", "salaryMinAnnualUsd": 150000,
              "salaryMaxAnnualUsd": 180000, "applicationDeadline": "2026-12-01", "workLocationCountry": "US"}]}
            """;
        var classifier = new FoundryJobPostingClassifier(new FakeChatClient(reply));

        var results = await classifier.ClassifyAsync(OnePosting(), CancellationToken.None);

        var result = Assert.Single(results);
        Assert.Equal(VisaSponsorshipStatus.Sponsors, result.VisaSponsorship);
        Assert.Equal(150000, result.SalaryMinAnnualUsd);
        Assert.Equal(180000, result.SalaryMaxAnnualUsd);
        Assert.Equal(new DateOnly(2026, 12, 1), result.ApplicationDeadline);
        Assert.Equal("US", result.WorkLocationCountry);
    }

    [Fact]
    public async Task ClassifyAsync_fails_open_to_unspecified_on_a_bare_array_response()
    {
        // If a model reply ever regresses to a bare top-level array again, this must degrade to
        // safe defaults rather than throw and take down the whole matching tick.
        const string reply = """[{"visaSponsorship": "NoSponsorship"}]""";
        var classifier = new FoundryJobPostingClassifier(new FakeChatClient(reply));

        var results = await classifier.ClassifyAsync(OnePosting(), CancellationToken.None);

        var result = Assert.Single(results);
        Assert.Equal(VisaSponsorshipStatus.Unspecified, result.VisaSponsorship);
        Assert.Null(result.SalaryMinAnnualUsd);
        Assert.Null(result.ApplicationDeadline);
    }

    [Fact]
    public async Task ClassifyAsync_fails_open_when_the_result_count_does_not_match_the_input_count()
    {
        const string reply =
            """{"classifications": [{"visaSponsorship": "Sponsors"}, {"visaSponsorship": "Sponsors"}]}""";
        var classifier = new FoundryJobPostingClassifier(new FakeChatClient(reply));

        var results = await classifier.ClassifyAsync(OnePosting(), CancellationToken.None);

        var result = Assert.Single(results); // one input in, one result out — never trust a misaligned batch
        Assert.Equal(VisaSponsorshipStatus.Unspecified, result.VisaSponsorship);
    }

    [Fact]
    public async Task ClassifyAsync_returns_empty_for_empty_input_without_calling_the_model()
    {
        var classifier = new FoundryJobPostingClassifier(new FakeChatClient("should never be read"));

        var results = await classifier.ClassifyAsync([], CancellationToken.None);

        Assert.Empty(results);
    }

}
