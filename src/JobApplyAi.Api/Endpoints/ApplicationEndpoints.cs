using JobApplyAi.Api.Services;
using JobApplyAi.Domain;
using JobApplyAi.Domain.Abstractions;
using JobApplyAi.Domain.Seed;
using JobApplyAi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace JobApplyAi.Api.Endpoints;

/// <summary>
/// The {matchResultId}-keyed routes exist for the future browser extension too, but the Blazor
/// dashboard calls ApplicationGenerationService directly in-process instead (same pattern as
/// ProfileActivationService) — no HTTP self-call. by-external-job/ is extension-only: a content
/// script on a live ATS page knows the (source, externalJobId) from the URL, not a matchResultId.
/// </summary>
public static class ApplicationEndpoints
{
    public record ApplicationDto(
        Guid? ApplicationId, string Status, DateTimeOffset? GeneratedAtUtc, DateTimeOffset? AppliedAtUtc,
        string? ResumeDownloadUrl, string? CoverLetterDownloadUrl);

    public record ExtensionContextDto(
        ExtensionJobPostingDto? JobPosting, Guid? MatchResultId, Guid? ApplicationId, string? ApplicationStatus,
        ExtensionProfileDto? Profile, string? ResumeDownloadUrl, string? CoverLetterDownloadUrl);

    public record ExtensionJobPostingDto(string Title, string CompanyName);

    public record ExtensionProfileDto(
        string? FirstName, string? LastName, string? Email, string? Phone, string? LinkedInUrl,
        string? PortfolioUrl, string? LocationText);

    public static IEndpointRouteBuilder MapApplicationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/applications");

        group.MapPost("/{matchResultId:guid}/generate", GenerateAsync);
        group.MapGet("/{matchResultId:guid}", GetAsync);
        group.MapPost("/{matchResultId:guid}/mark-applied", MarkAppliedAsync);
        group.MapGet("/by-external-job/{source}/{externalJobId}", GetByExternalJobAsync);

        return app;
    }

    private static async Task<IResult> GenerateAsync(
        Guid matchResultId, ApplicationGenerationService generationService, CancellationToken ct)
    {
        try
        {
            var application = await generationService.GenerateAsync(matchResultId, ct);
            return Results.Ok(new { applicationId = application.Id, status = application.Status.ToString() });
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
    }

    private static async Task<IResult> GetAsync(
        Guid matchResultId, AppDbContext db, IBlobStorageService blobStorage, CancellationToken ct)
    {
        var application = await db.Applications.FirstOrDefaultAsync(a => a.MatchResultId == matchResultId, ct);
        if (application is null)
        {
            return Results.Ok(new ApplicationDto(null, "Matched", null, null, null, null));
        }

        var (resumeUrl, coverLetterUrl) = await GetDownloadUrlsIfReadyAsync(application, blobStorage, ct);

        return Results.Ok(new ApplicationDto(
            application.Id, application.Status.ToString(), application.GeneratedAtUtc, application.AppliedAtUtc,
            resumeUrl, coverLetterUrl));
    }

    private static async Task<IResult> MarkAppliedAsync(
        Guid matchResultId, ApplicationGenerationService generationService, CancellationToken ct)
    {
        try
        {
            var application = await generationService.MarkAppliedAsync(matchResultId, ct);
            return Results.Ok(new { status = application.Status.ToString() });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
    }

    private static async Task<IResult> GetByExternalJobAsync(
        string source, string externalJobId, AppDbContext db, IBlobStorageService blobStorage, CancellationToken ct)
    {
        if (!Enum.TryParse<JobSource>(source, ignoreCase: true, out var jobSource))
        {
            return Results.BadRequest(new { error = $"Unknown source '{source}'." });
        }

        // Profile data is returned even if this exact posting was never polled/matched — the
        // extension is useful as a generic autofiller on any Greenhouse/Lever page, not just ones
        // the matching pipeline already found. Only contact fields are needed here — no
        // WorkExperiences/Educations Include, since no content script fills anything from them.
        var profile = await db.CandidateProfiles
            .FirstOrDefaultAsync(p => p.UserId == SeedData.DefaultUserId && p.Status == ProfileStatus.Active, ct);
        var profileDto = profile is null ? null : ToExtensionProfileDto(profile);

        var jobPosting = await db.JobPostings
            .FirstOrDefaultAsync(j => j.Source == jobSource && j.ExternalJobId == externalJobId, ct);
        if (jobPosting is null)
        {
            return Results.Ok(new ExtensionContextDto(null, null, null, null, profileDto, null, null));
        }

        var match = await db.MatchResults.FirstOrDefaultAsync(
            m => m.UserId == SeedData.DefaultUserId && m.JobPostingId == jobPosting.Id, ct);

        Guid? applicationId = null;
        string? applicationStatus = null;
        string? resumeUrl = null;
        string? coverLetterUrl = null;

        if (match is not null)
        {
            var application = await db.Applications.FirstOrDefaultAsync(a => a.MatchResultId == match.Id, ct);
            if (application is not null)
            {
                applicationId = application.Id;
                applicationStatus = application.Status.ToString();
                (resumeUrl, coverLetterUrl) = await GetDownloadUrlsIfReadyAsync(application, blobStorage, ct);
            }
        }

        return Results.Ok(new ExtensionContextDto(
            new ExtensionJobPostingDto(jobPosting.Title, jobPosting.CompanyName),
            match?.Id, applicationId, applicationStatus, profileDto, resumeUrl, coverLetterUrl));
    }

    private static async Task<(string? ResumeUrl, string? CoverLetterUrl)> GetDownloadUrlsIfReadyAsync(
        Domain.Entities.Application application, IBlobStorageService blobStorage, CancellationToken ct)
    {
        if (application.Status is not (ApplicationStatus.Prepped or ApplicationStatus.Applied))
        {
            return (null, null);
        }

        var resumeUrl = (await blobStorage.GetDownloadUrlAsync(
            BlobContainers.Generated, ApplicationGenerationService.ResumeBlobPath(application.UserId, application.Id),
            TimeSpan.FromMinutes(30), ct)).ToString();
        var coverLetterUrl = (await blobStorage.GetDownloadUrlAsync(
            BlobContainers.Generated, ApplicationGenerationService.CoverLetterBlobPath(application.UserId, application.Id),
            TimeSpan.FromMinutes(30), ct)).ToString();
        return (resumeUrl, coverLetterUrl);
    }

    private static ExtensionProfileDto ToExtensionProfileDto(Domain.Entities.CandidateProfile profile)
    {
        // No separate first/last name fields on the profile — split on the first space as a
        // best-effort heuristic for ATS forms that want them separately.
        var nameParts = (profile.FullName ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

        return new ExtensionProfileDto(
            nameParts.Length > 0 ? nameParts[0] : null,
            nameParts.Length > 1 ? nameParts[1] : null,
            profile.Email, profile.Phone, profile.LinkedInUrl, profile.PortfolioUrl, profile.LocationText);
    }
}
