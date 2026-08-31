using System.Threading.Channels;
using JobApplyAi.Domain;
using JobApplyAi.Domain.Abstractions;
using JobApplyAi.Domain.Entities;
using JobApplyAi.Infrastructure.Data;

namespace JobApplyAi.Api.BackgroundServices;

public sealed record ResumeParseJob(Guid ProfileId, byte[] Content, string FileName, string ContentType);

/// <summary>
/// In-process parse queue. Jobs die with the process (single-user tolerable) — the
/// stuck-Parsing timeout in the status endpoint is the recovery path, not retries here.
/// </summary>
public class ResumeParsingWorker(
    Channel<ResumeParseJob> queue,
    IServiceScopeFactory scopeFactory,
    ILogger<ResumeParsingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queue.Reader.ReadAllAsync(stoppingToken))
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var parser = scope.ServiceProvider.GetRequiredService<IResumeParser>();

            var profile = await db.CandidateProfiles.FindAsync([job.ProfileId], stoppingToken);
            if (profile is null || profile.Status != ProfileStatus.Parsing)
            {
                continue;
            }

            try
            {
                try
                {
                    var parsed = await parser.ParseAsync(job.Content, job.FileName, job.ContentType, stoppingToken);
                    ApplyParsedResume(db, profile, parsed);
                    profile.Status = ProfileStatus.NeedsReview;
                    profile.ParsedAtUtc = DateTimeOffset.UtcNow;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Parsing resume for profile {ProfileId} failed.", job.ProfileId);
                    profile.Status = ProfileStatus.Failed;
                }

                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A save failure (transient DB fault, concurrent modification, etc.) must never
                // kill the worker — that would silently stop all future resume parsing until
                // the app restarts. Log and move on to the next queued job.
                logger.LogError(ex, "Saving parsed profile {ProfileId} failed.", job.ProfileId);
            }
        }
    }

    private static void ApplyParsedResume(AppDbContext db, CandidateProfile profile, ParsedResume parsed)
    {
        profile.FullName = parsed.FullName;
        profile.Email = parsed.Email;
        profile.Phone = parsed.Phone;
        profile.LocationText = parsed.LocationText;
        profile.LinkedInUrl = parsed.LinkedInUrl;
        profile.PortfolioUrl = parsed.PortfolioUrl;
        profile.SummaryText = parsed.SummaryText;

        // Explicitly Add() every new child — these have client-set Guid keys and are reached
        // only via navigation assignment, never through db.Add(). Left to EF's own graph-fixup
        // heuristic, entities in that shape can be inferred Modified instead of Added, producing
        // an UPDATE that matches 0 rows (DbUpdateConcurrencyException) since the row never
        // existed. Explicit Add() removes the ambiguity outright.
        var workExperiences = parsed.WorkExperiences.Select(w => new ProfileWorkExperience
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
        profile.WorkExperiences = workExperiences;
        db.ProfileWorkExperiences.AddRange(workExperiences);

        var educations = parsed.Educations.Select(e => new ProfileEducation
        {
            Id = Guid.NewGuid(),
            CandidateProfileId = profile.Id,
            Institution = e.Institution,
            Degree = e.Degree,
            FieldOfStudy = e.FieldOfStudy,
            StartDate = e.StartDate,
            EndDate = e.EndDate,
        }).ToList();
        profile.Educations = educations;
        db.ProfileEducations.AddRange(educations);

        var skills = parsed.Skills.Select(s => new ProfileSkill
        {
            Id = Guid.NewGuid(),
            CandidateProfileId = profile.Id,
            Name = s.Name,
            Category = s.Category,
        }).ToList();
        profile.Skills = skills;
        db.ProfileSkills.AddRange(skills);
    }
}
