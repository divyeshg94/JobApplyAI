namespace JobApplyAi.Domain.Abstractions;

/// <summary>LLM rescore step: judges fit between a candidate profile and one job posting.</summary>
public interface IMatchScorer
{
    Task<MatchScore> ScoreAsync(
        string profileSummaryText, string jobTitle, string companyName, string jobDescriptionText, CancellationToken ct);
}

public sealed record MatchScore(double Score, string Reasoning);
