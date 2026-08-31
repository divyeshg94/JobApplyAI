using System.Text.Json;
using JobApplyAi.Domain.Abstractions;
using JobApplyAi.Infrastructure.JobSources;
using Microsoft.Extensions.AI;

namespace JobApplyAi.Infrastructure.Ai;

public class FoundryResumeParser(IChatClient chatClient) : IResumeParser
{
    private const string SystemPrompt =
        """
        You extract structured data from resumes. Respond with ONLY a JSON object, no markdown fences, matching:
        {
          "fullName": string|null, "email": string|null, "phone": string|null,
          "locationText": string|null, "linkedInUrl": string|null, "portfolioUrl": string|null,
          "summaryText": string|null,
          "workExperiences": [{"company": string, "title": string, "locationText": string|null,
            "startDate": "YYYY-MM-DD"|null, "endDate": "YYYY-MM-DD"|null, "isCurrent": bool,
            "descriptionText": string|null}],
          "educations": [{"institution": string, "degree": string|null, "fieldOfStudy": string|null,
            "startDate": "YYYY-MM-DD"|null, "endDate": "YYYY-MM-DD"|null}],
          "skills": [{"name": string, "category": string|null}]
        }
        Dates: use the first day of the month when only month/year is given. Omit nothing you find;
        invent nothing you don't. summaryText: the candidate's own summary if present, else a 2-3
        sentence synthesis of their experience.
        """;

    public async Task<ParsedResume> ParseAsync(byte[] content, string fileName, string contentType, CancellationToken ct)
    {
        var text = ResumeTextExtractor.Extract(content, fileName, contentType);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException($"No text could be extracted from '{fileName}'.");
        }

        var response = await chatClient.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, SystemPrompt),
                new ChatMessage(ChatRole.User, text),
            ],
            new ChatOptions { ResponseFormat = ChatResponseFormat.Json, Temperature = 0 },
            ct);

        var json = ChatJsonHelper.StripMarkdownFences(response.Text);
        return JsonSerializer.Deserialize<ParsedResume>(json, JsonDefaults.Options)
            ?? throw new InvalidOperationException("Resume parse returned empty JSON.");
    }
}
