using System.Threading.Channels;
using JobApplyAi.Api.BackgroundServices;
using JobApplyAi.Api.Options;
using JobApplyAi.Domain;
using JobApplyAi.Domain.Abstractions;
using JobApplyAi.Domain.Entities;
using JobApplyAi.Domain.Seed;
using JobApplyAi.Infrastructure.Data;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace JobApplyAi.Api.Endpoints;

public static class ProfileEndpoints
{
    public record WorkExperienceDto(
        string Company, string Title, string? LocationText,
        DateOnly? StartDate, DateOnly? EndDate, bool IsCurrent, string? DescriptionText);

    public record EducationDto(
        string Institution, string? Degree, string? FieldOfStudy, DateOnly? StartDate, DateOnly? EndDate);

    public record SkillDto(string Name, string? Category);

    public record ProfileUpdateRequest(
        string? FullName, string? Email, string? Phone, string? LocationText,
        string? LinkedInUrl, string? PortfolioUrl, string? SummaryText,
        bool RequiresVisaSponsorship, int? MinimumSalaryUsd, string? RequiredCountry, List<string> ExcludedCompanies,
        List<WorkExperienceDto> WorkExperiences, List<EducationDto> Educations, List<SkillDto> Skills);

    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profile");

        group.MapPost("/resume", UploadResumeAsync).DisableAntiforgery();
        group.MapGet("/{id:guid}/status", GetStatusAsync);
        group.MapGet("/active", GetActiveAsync);
        group.MapGet("/{id:guid}", GetProfileAsync);
        group.MapPut("/{id:guid}", UpdateProfileAsync);
        group.MapPost("/{id:guid}/confirm", ConfirmProfileAsync);

        return app;
    }

    private static async Task<IResult> UploadResumeAsync(
        IFormFile file,
        AppDbContext db,
        IBlobStorageService blobStorage,
        Channel<ResumeParseJob> queue,
        IOptions<ParsingOptions> options,
        CancellationToken ct)
    {
        if (file.Length == 0 || file.Length > options.Value.MaxUploadBytes)
        {
            return Results.BadRequest(new { error = $"File must be 1..{options.Value.MaxUploadBytes} bytes." });
        }

        var profile = new CandidateProfile
        {
            Id = Guid.NewGuid(),
            UserId = SeedData.DefaultUserId,
            Status = ProfileStatus.Parsing,
            RawResumeFileName = file.FileName,
            RawResumeContentType = file.ContentType,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        using var memory = new MemoryStream();
        await file.CopyToAsync(memory, ct);
        var bytes = memory.ToArray();

        memory.Position = 0;
        profile.RawResumeBlobUrl = await blobStorage.UploadAsync(
            BlobContainers.Resumes,
            $"raw/{profile.UserId}/{profile.Id}/{Path.GetFileName(file.FileName)}",
            memory,
            file.ContentType,
            ct);

        db.CandidateProfiles.Add(profile);
        await db.SaveChangesAsync(ct);

        await queue.Writer.WriteAsync(new ResumeParseJob(profile.Id, bytes, file.FileName, file.ContentType), ct);
        return Results.Accepted($"/api/profile/{profile.Id}/status", new { profileId = profile.Id });
    }

    private static async Task<IResult> GetStatusAsync(
        Guid id, AppDbContext db, IOptions<ParsingOptions> options, CancellationToken ct)
    {
        var profile = await db.CandidateProfiles
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == SeedData.DefaultUserId, ct);
        if (profile is null)
        {
            return Results.NotFound();
        }

        // Stuck-state recovery: a parse the worker never finished (process restart) fails here
        // instead of spinning forever.
        if (profile.Status == ProfileStatus.Parsing
            && DateTimeOffset.UtcNow - profile.CreatedAtUtc > TimeSpan.FromMinutes(options.Value.TimeoutMinutes))
        {
            profile.Status = ProfileStatus.Failed;
            await db.SaveChangesAsync(ct);
        }

        return Results.Ok(new { status = profile.Status.ToString() });
    }

    private static async Task<IResult> GetActiveAsync(AppDbContext db, CancellationToken ct)
    {
        var profile = await LoadFullProfileAsync(db, null, ProfileStatus.Active, ct);
        return profile is null ? Results.NotFound() : Results.Ok(ToDto(profile));
    }

    private static async Task<IResult> GetProfileAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var profile = await LoadFullProfileAsync(db, id, null, ct);
        return profile is null ? Results.NotFound() : Results.Ok(ToDto(profile));
    }

    private static async Task<IResult> UpdateProfileAsync(
        Guid id, ProfileUpdateRequest request, AppDbContext db, CancellationToken ct)
    {
        var profile = await LoadFullProfileAsync(db, id, null, ct);
        if (profile is null)
        {
            return Results.NotFound();
        }

        profile.FullName = request.FullName;
        profile.Email = request.Email;
        profile.Phone = request.Phone;
        profile.LocationText = request.LocationText;
        profile.LinkedInUrl = request.LinkedInUrl;
        profile.PortfolioUrl = request.PortfolioUrl;
        profile.SummaryText = request.SummaryText;
        profile.RequiresVisaSponsorship = request.RequiresVisaSponsorship;
        profile.MinimumSalaryUsd = request.MinimumSalaryUsd;
        profile.RequiredCountry = string.IsNullOrWhiteSpace(request.RequiredCountry)
            ? null
            : request.RequiredCountry.Trim().ToUpperInvariant();

        // Replace-all children — simplest correct semantics for a single-user edit form.
        // Explicit db.<Set>.AddRange() alongside the navigation Add() matters: new children have
        // client-set Guid keys and, left to EF's own graph-fixup heuristic, can be inferred
        // Modified instead of Added — producing an UPDATE that matches 0 rows
        // (DbUpdateConcurrencyException) since the row never existed.
        profile.WorkExperiences.Clear();
        var newWorkExperiences = request.WorkExperiences.Select(w => new ProfileWorkExperience
        {
            Id = Guid.NewGuid(),
            CandidateProfileId = profile.Id,
            Company = w.Company,
            Title = w.Title,
            LocationText = w.LocationText,
            StartDate = w.StartDate,
            EndDate = w.EndDate,
            IsCurrent = w.IsCurrent,
            DescriptionText = w.DescriptionText,
        }).ToList();
        profile.WorkExperiences.AddRange(newWorkExperiences);
        db.ProfileWorkExperiences.AddRange(newWorkExperiences);

        profile.Educations.Clear();
        var newEducations = request.Educations.Select(e => new ProfileEducation
        {
            Id = Guid.NewGuid(),
            CandidateProfileId = profile.Id,
            Institution = e.Institution,
            Degree = e.Degree,
            FieldOfStudy = e.FieldOfStudy,
            StartDate = e.StartDate,
            EndDate = e.EndDate,
        }).ToList();
        profile.Educations.AddRange(newEducations);
        db.ProfileEducations.AddRange(newEducations);

        profile.Skills.Clear();
        var newSkills = request.Skills.Select(s => new ProfileSkill
        {
            Id = Guid.NewGuid(),
            CandidateProfileId = profile.Id,
            Name = s.Name,
            Category = s.Category,
        }).ToList();
        profile.Skills.AddRange(newSkills);
        db.ProfileSkills.AddRange(newSkills);

        profile.ExcludedCompanies.Clear();
        var newExcludedCompanies = request.ExcludedCompanies.Select(name => new ProfileExcludedCompany
        {
            Id = Guid.NewGuid(),
            CandidateProfileId = profile.Id,
            CompanyName = name,
        }).ToList();
        profile.ExcludedCompanies.AddRange(newExcludedCompanies);
        db.ProfileExcludedCompanies.AddRange(newExcludedCompanies);

        if (profile.Status == ProfileStatus.Failed)
        {
            profile.Status = ProfileStatus.NeedsReview;
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDto(profile));
    }

    private static async Task<IResult> ConfirmProfileAsync(
        Guid id,
        AppDbContext db,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        CancellationToken ct)
    {
        var exists = await db.CandidateProfiles
            .AnyAsync(p => p.Id == id && p.UserId == SeedData.DefaultUserId, ct);
        if (!exists)
        {
            return Results.NotFound();
        }

        try
        {
            await Services.ProfileActivationService.ActivateAsync(db, id, embeddingGenerator, ct);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }

        return Results.Ok(new { status = ProfileStatus.Active.ToString() });
    }

    private static async Task<CandidateProfile?> LoadFullProfileAsync(
        AppDbContext db, Guid? id, ProfileStatus? status, CancellationToken ct)
        => await db.CandidateProfiles
            .Include(p => p.WorkExperiences)
            .Include(p => p.Educations)
            .Include(p => p.Skills)
            .Include(p => p.ExcludedCompanies)
            .FirstOrDefaultAsync(p =>
                p.UserId == SeedData.DefaultUserId
                && (id == null || p.Id == id)
                && (status == null || p.Status == status), ct);

    private static object ToDto(CandidateProfile profile) => new
    {
        profile.Id,
        Status = profile.Status.ToString(),
        profile.FullName,
        profile.Email,
        profile.Phone,
        profile.LocationText,
        profile.LinkedInUrl,
        profile.PortfolioUrl,
        profile.SummaryText,
        profile.RequiresVisaSponsorship,
        profile.MinimumSalaryUsd,
        profile.RequiredCountry,
        profile.RawResumeFileName,
        WorkExperiences = profile.WorkExperiences.Select(w => new WorkExperienceDto(
            w.Company, w.Title, w.LocationText, w.StartDate, w.EndDate, w.IsCurrent, w.DescriptionText)),
        Educations = profile.Educations.Select(e => new EducationDto(
            e.Institution, e.Degree, e.FieldOfStudy, e.StartDate, e.EndDate)),
        Skills = profile.Skills.Select(s => new SkillDto(s.Name, s.Category)),
        ExcludedCompanies = profile.ExcludedCompanies.Select(c => c.CompanyName),
    };
}
