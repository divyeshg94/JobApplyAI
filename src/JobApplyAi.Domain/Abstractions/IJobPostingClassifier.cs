namespace JobApplyAi.Domain.Abstractions;

/// <summary>
/// Extracts hard-filter facts from a posting's description text, once per posting. Batched — with
/// hundreds of postings needing classification, one LLM call per posting would be wasteful.
/// Returns exactly one result per input, same order.
/// </summary>
public interface IJobPostingClassifier
{
    Task<IReadOnlyList<JobPostingClassification>> ClassifyAsync(
        IReadOnlyList<JobPostingClassificationInput> postings, CancellationToken ct);
}

public sealed record JobPostingClassificationInput(
    string Title, string CompanyName, string? LocationText, string DescriptionText);

public sealed record JobPostingClassification(
    VisaSponsorshipStatus VisaSponsorship, int? SalaryMinAnnualUsd, int? SalaryMaxAnnualUsd,
    DateOnly? ApplicationDeadline, string? WorkLocationCountry);
