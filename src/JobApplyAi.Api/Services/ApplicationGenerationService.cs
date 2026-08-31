using JobApplyAi.Domain;
using JobApplyAi.Domain.Abstractions;
using JobApplyAi.Domain.Entities;
using JobApplyAi.Infrastructure.Data;
using JobApplyAi.Infrastructure.Documents;
using Microsoft.EntityFrameworkCore;

namespace JobApplyAi.Api.Services;

/// <summary>
/// Milestone 6: generates a tailored resume + cover letter for one match, renders both to PDF,
/// uploads to Blob, and tracks the Application lifecycle (Matched → Prepped → Applied). Shared by
/// the REST endpoint (for the future extension, M7) and the Blazor dashboard, same pattern as
/// ProfileActivationService.
/// </summary>
public class ApplicationGenerationService(
    AppDbContext db,
    IApplicationDocumentGenerator documentGenerator,
    IBlobStorageService blobStorage)
{
    public static string ResumeBlobPath(Guid userId, Guid applicationId) => $"{userId}/{applicationId}/resume.pdf";
    public static string CoverLetterBlobPath(Guid userId, Guid applicationId) => $"{userId}/{applicationId}/cover-letter.pdf";

    public async Task<Application> GenerateAsync(Guid matchResultId, CancellationToken ct)
    {
        var match = await db.MatchResults
            .Include(m => m.JobPosting)
            .Include(m => m.CandidateProfile!).ThenInclude(p => p.WorkExperiences)
            .Include(m => m.CandidateProfile!).ThenInclude(p => p.Educations)
            .Include(m => m.CandidateProfile!).ThenInclude(p => p.Skills)
            .FirstOrDefaultAsync(m => m.Id == matchResultId, ct)
            ?? throw new InvalidOperationException($"Match {matchResultId} not found.");

        var jobPosting = match.JobPosting ?? throw new InvalidOperationException("Match has no linked job posting.");
        var profile = match.CandidateProfile ?? throw new InvalidOperationException("Match has no linked candidate profile.");

        var tailored = await documentGenerator.GenerateAsync(profile, jobPosting, ct);
        var resumeBytes = QuestPdfDocumentRenderer.RenderResume(profile, tailored);
        var coverLetterBytes = QuestPdfDocumentRenderer.RenderCoverLetter(profile, jobPosting, tailored.CoverLetterText);

        var application = await db.Applications.FirstOrDefaultAsync(a => a.MatchResultId == matchResultId, ct);
        var isNew = application is null;
        application ??= new Application { Id = Guid.NewGuid(), UserId = match.UserId, MatchResultId = matchResultId };

        using (var resumeStream = new MemoryStream(resumeBytes))
        {
            application.TailoredResumeBlobUrl = await blobStorage.UploadAsync(
                BlobContainers.Generated, ResumeBlobPath(match.UserId, application.Id), resumeStream, "application/pdf", ct);
        }

        using (var coverLetterStream = new MemoryStream(coverLetterBytes))
        {
            application.TailoredCoverLetterBlobUrl = await blobStorage.UploadAsync(
                BlobContainers.Generated, CoverLetterBlobPath(match.UserId, application.Id), coverLetterStream, "application/pdf", ct);
        }

        application.Status = ApplicationStatus.Prepped;
        application.GeneratedAtUtc = DateTimeOffset.UtcNow;

        if (isNew)
        {
            db.Applications.Add(application);
        }

        await db.SaveChangesAsync(ct);
        return application;
    }

    public async Task<Application> MarkAppliedAsync(Guid matchResultId, CancellationToken ct)
    {
        var application = await db.Applications.FirstOrDefaultAsync(a => a.MatchResultId == matchResultId, ct)
            ?? throw new InvalidOperationException("Generate documents before marking this match applied.");

        application.Status = ApplicationStatus.Applied;
        application.AppliedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return application;
    }
}
