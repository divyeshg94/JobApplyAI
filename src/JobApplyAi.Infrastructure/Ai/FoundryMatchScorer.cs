using System.Text.Json;
using JobApplyAi.Domain.Abstractions;
using JobApplyAi.Infrastructure.JobSources;
using Microsoft.Extensions.AI;

namespace JobApplyAi.Infrastructure.Ai;

public class FoundryMatchScorer(IChatClient chatClient) : IMatchScorer
{
    private const string SystemPrompt =
        """
        You score how well a candidate profile fits a job posting. Respond with ONLY a JSON
        object, no markdown fences, matching: {"score": integer 0-100, "reasoning": string}.
        Score genuine skills/experience alignment — be discriminating, not generically positive;
        most postings are not a great fit and should score accordingly. reasoning: 1-3 sentences
        citing specific overlaps or gaps, not generic praise.
        """;

    public async Task<MatchScore> ScoreAsync(
        string profileSummaryText, string jobTitle, string companyName, string jobDescriptionText, CancellationToken ct)
    {
        var userMessage = $"""
            Candidate profile:
            {profileSummaryText}

            Job posting:
            {jobTitle} at {companyName}

            {jobDescriptionText}
            """;

        var response = await chatClient.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, SystemPrompt),
                new ChatMessage(ChatRole.User, userMessage),
            ],
            new ChatOptions { ResponseFormat = ChatResponseFormat.Json, Temperature = 0 },
            ct);

        var json = ChatJsonHelper.StripMarkdownFences(response.Text);
        var result = JsonSerializer.Deserialize<ScoreResponse>(json, JsonDefaults.Options)
            ?? throw new InvalidOperationException("Match scoring returned empty JSON.");
        return new MatchScore(result.Score, result.Reasoning);
    }

    private sealed record ScoreResponse(double Score, string Reasoning);
}
