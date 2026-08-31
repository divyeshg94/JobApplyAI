using JobApplyAi.Infrastructure.Ai;
using Microsoft.Extensions.AI;

namespace JobApplyAi.Infrastructure.Tests.Ai;

public class FoundryResumeParserTests
{
    private const string ValidJson =
        """
        {
          "fullName": "Jane Doe", "email": "jane@example.com", "phone": "+31 6 12345678",
          "locationText": "Amsterdam", "linkedInUrl": null, "portfolioUrl": null,
          "summaryText": "Senior engineer.",
          "workExperiences": [{"company": "Acme", "title": "Engineer", "locationText": null,
            "startDate": "2020-01-01", "endDate": null, "isCurrent": true, "descriptionText": "Built stuff."}],
          "educations": [{"institution": "TU Delft", "degree": "MSc", "fieldOfStudy": "CS",
            "startDate": "2015-09-01", "endDate": "2017-07-01"}],
          "skills": [{"name": "C#", "category": "Language"}]
        }
        """;

    private static byte[] MinimalDocx()
    {
        using var stream = new MemoryStream();
        using (var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(
            stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(
                new DocumentFormat.OpenXml.Wordprocessing.Body(
                    new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                        new DocumentFormat.OpenXml.Wordprocessing.Run(
                            new DocumentFormat.OpenXml.Wordprocessing.Text("Jane Doe resume text")))));
        }

        return stream.ToArray();
    }

    private sealed class FakeChatClient(string reply) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    [Fact]
    public async Task ParseAsync_maps_llm_json_to_parsed_resume()
    {
        var parser = new FoundryResumeParser(new FakeChatClient(ValidJson));

        var result = await parser.ParseAsync(MinimalDocx(), "resume.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", CancellationToken.None);

        Assert.Equal("Jane Doe", result.FullName);
        Assert.Equal("jane@example.com", result.Email);
        var work = Assert.Single(result.WorkExperiences);
        Assert.Equal("Acme", work.Company);
        Assert.True(work.IsCurrent);
        Assert.Equal(new DateOnly(2020, 1, 1), work.StartDate);
        Assert.Null(work.EndDate);
        var education = Assert.Single(result.Educations);
        Assert.Equal("TU Delft", education.Institution);
        var skill = Assert.Single(result.Skills);
        Assert.Equal("C#", skill.Name);
    }

    [Fact]
    public async Task ParseAsync_strips_markdown_fences_from_reply()
    {
        var parser = new FoundryResumeParser(new FakeChatClient($"```json\n{ValidJson}\n```"));

        var result = await parser.ParseAsync(MinimalDocx(), "resume.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", CancellationToken.None);

        Assert.Equal("Jane Doe", result.FullName);
    }

    [Fact]
    public async Task ParseAsync_rejects_unsupported_format()
    {
        var parser = new FoundryResumeParser(new FakeChatClient(ValidJson));

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            parser.ParseAsync([1, 2, 3], "resume.txt", "text/plain", CancellationToken.None));
    }
}
