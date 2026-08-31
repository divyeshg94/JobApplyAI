using JobApplyAi.Api.Services;
using JobApplyAi.Domain;
using JobApplyAi.Domain.Abstractions;
using JobApplyAi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace JobApplyAi.Api.Endpoints;

/// <summary>
/// For the future browser extension (M7) — the Blazor dashboard calls ApplicationGenerationService
/// directly in-process instead (same pattern as ProfileActivationService), no HTTP self-call.
/// </summary>
public static class ApplicationEndpoints
{
    public record ApplicationDto(
        Guid? ApplicationId, string Status, DateTimeOffset? GeneratedAtUtc, DateTimeOffset? AppliedAtUtc,
        string? ResumeDownloadUrl, string? CoverLetterDownloadUrl);

    public static IEndpointRouteBuilder MapApplicationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/applications");

        group.MapPost("/{matchResultId:guid}/generate", GenerateAsync);
        group.MapGet("/{matchResultId:guid}", GetAsync);
        group.MapPost("/{matchResultId:guid}/mark-applied", MarkAppliedAsync);

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

        string? resumeUrl = null;
        string? coverLetterUrl = null;
        if (application.Status is ApplicationStatus.Prepped or ApplicationStatus.Applied)
        {
            resumeUrl = (await blobStorage.GetDownloadUrlAsync(
                BlobContainers.Generated, ApplicationGenerationService.ResumeBlobPath(application.UserId, application.Id),
                TimeSpan.FromMinutes(30), ct)).ToString();
            coverLetterUrl = (await blobStorage.GetDownloadUrlAsync(
                BlobContainers.Generated, ApplicationGenerationService.CoverLetterBlobPath(application.UserId, application.Id),
                TimeSpan.FromMinutes(30), ct)).ToString();
        }

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
}
