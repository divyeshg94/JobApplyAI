using System.Text.Json;
using System.Text.Json.Serialization;
using JobApplyAi.Domain;
using JobApplyAi.Domain.Abstractions;
using JobApplyAi.Infrastructure.JobSources;
using Microsoft.Extensions.AI;

namespace JobApplyAi.Infrastructure.Ai;

public class FoundryJobPostingClassifier(IChatClient chatClient) : IJobPostingClassifier
{
    private const string SystemPrompt =
        """
        For each numbered job posting, extract hard-filter facts from its stated location and
        description text. Respond with ONLY a JSON object, no markdown fences, matching:
        {"classifications": [{"visaSponsorship": "Sponsors"|"NoSponsorship"|"Unspecified",
          "salaryMinAnnualUsd": integer|null, "salaryMaxAnnualUsd": integer|null,
          "applicationDeadline": "YYYY-MM-DD"|null, "workLocationCountry": "US"|null}]}
        The classifications array must have exactly one entry per posting, in the SAME ORDER given.

        visaSponsorship: "NoSponsorship" ONLY if the posting explicitly states it cannot or will
        not sponsor work visas (e.g. "must be authorized to work in the US without sponsorship now
        or in the future", "unable to sponsor visas"). "Sponsors" only if it explicitly offers or
        mentions visa/H1B sponsorship. Otherwise, including when simply unmentioned, "Unspecified"
        — do not infer NoSponsorship from silence.

        salary: only if the posting states a figure in USD. Normalize hourly to annual (hours *
        2080). If only one figure is given, use it for both min and max. If the currency is not
        USD, or no figure is given, use null for both — never guess or convert currencies.

        applicationDeadline: only if the posting explicitly states a closing/deadline date for
        applications (e.g. "applications accepted until July 30, 2026"). Use null if no deadline
        is mentioned (most postings are open-ended). If a date is given without a year, assume the
        nearest future occurrence of that month/day.

        workLocationCountry: the ISO 3166-1 alpha-2 code (e.g. "US", "IN", "GB") ONLY when the
        posting confidently restricts work to ONE specific country — read both the given location
        field and the description (e.g. "Remote - India" means "IN"; "Remote (US only)" means
        "US"). Use null if the posting is open to multiple countries, says just "Remote" with no
        country qualifier, or the location is genuinely ambiguous — do not guess.
        """;

    public async Task<IReadOnlyList<JobPostingClassification>> ClassifyAsync(
        IReadOnlyList<JobPostingClassificationInput> postings, CancellationToken ct)
    {
        if (postings.Count == 0)
        {
            return [];
        }

        var userMessage = string.Join("\n\n", postings.Select((p, i) =>
            $"{i + 1}. {p.Title} at {p.CompanyName}\nLocation: {p.LocationText ?? "(not given)"}\n{p.DescriptionText}"));

        var response = await chatClient.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, SystemPrompt),
                new ChatMessage(ChatRole.User, userMessage),
            ],
            new ChatOptions { ResponseFormat = ChatResponseFormat.Json, Temperature = 0 },
            ct);

        var json = ChatJsonHelper.StripMarkdownFences(response.Text);
        var results = JsonSerializer.Deserialize<ClassificationResponse>(json, JsonDefaults.Options)?.Classifications;

        if (results is null || results.Count != postings.Count)
        {
            // Malformed/mismatched response — fail open (Unspecified/no salary/deadline/country
            // data) rather than wrongly excluding postings or throwing away the whole batch.
            return postings.Select(_ => new JobPostingClassification(VisaSponsorshipStatus.Unspecified, null, null, null, null)).ToList();
        }

        return results.Select(r =>
        {
            var status = Enum.TryParse<VisaSponsorshipStatus>(r.VisaSponsorship, out var parsed)
                ? parsed
                : VisaSponsorshipStatus.Unspecified;
            var deadline = DateOnly.TryParse(r.ApplicationDeadline, out var parsedDeadline)
                ? parsedDeadline
                : (DateOnly?)null;
            return new JobPostingClassification(status, r.SalaryMinAnnualUsd, r.SalaryMaxAnnualUsd, deadline, r.WorkLocationCountry);
        }).ToList();
    }

    private sealed record ClassificationResponse(
        [property: JsonPropertyName("classifications")] List<ClassificationResult> Classifications);

    private sealed record ClassificationResult(
        [property: JsonPropertyName("visaSponsorship")] string VisaSponsorship,
        [property: JsonPropertyName("salaryMinAnnualUsd")] int? SalaryMinAnnualUsd,
        [property: JsonPropertyName("salaryMaxAnnualUsd")] int? SalaryMaxAnnualUsd,
        [property: JsonPropertyName("applicationDeadline")] string? ApplicationDeadline,
        [property: JsonPropertyName("workLocationCountry")] string? WorkLocationCountry);
}
