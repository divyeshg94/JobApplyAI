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

namespace JobApplyAi.Api.Services;

/// <summary>
/// Milestone 4, docs/architecture.md §5 steps 5-7: embed new job postings, vector-prefilter
/// against the active profile, LLM-rescore the top candidates. Called once per polling tick,
/// after new postings are persisted, from the same DbContext/scope as the poll loop.
/// </summary>
public class MatchingPipelineService(
    AppDbContext db,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    IMatchScorer matchScorer,
    IJobPostingClassifier postingClassifier,
    IOptions<MatchingOptions> options,
    ILogger<MatchingPipelineService> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var activeProfile = await db.CandidateProfiles
            .Include(p => p.WorkExperiences)
            .Include(p => p.Skills)
            .Include(p => p.ExcludedCompanies)
            .FirstOrDefaultAsync(p => p.UserId == SeedData.DefaultUserId && p.Status == ProfileStatus.Active, ct);
        if (activeProfile is null)
        {
            return;
        }

        var profileEmbedding = db.Entry(activeProfile)
            .Property<SqlVector<float>?>(AppDbContext.ProfileEmbeddingColumn).CurrentValue;
        if (profileEmbedding is null)
        {
            logger.LogWarning("Active profile {ProfileId} has no embedding; skipping matching this tick.", activeProfile.Id);
            return;
        }

        await EmbedMissingJobPostingsAsync(ct);
        await ClassifyPostingsAsync(ct);

        var profileSummary = ProfileActivationService.BuildEmbeddingText(activeProfile);
        var excludedCompanies = activeProfile.ExcludedCompanies.Select(c => c.CompanyName).ToList();
        var candidates = await PrefilterCandidatesAsync(
            profileEmbedding.Value, activeProfile.RequiresVisaSponsorship, activeProfile.MinimumSalaryUsd,
            activeProfile.RequiredCountry, excludedCompanies, ct);

        foreach (var (job, distance) in candidates)
        {
            try
            {
                var descriptionText = Truncate(job.DescriptionText ?? "", options.Value.MaxDescriptionChars);
                var score = await matchScorer.ScoreAsync(profileSummary, job.Title, job.CompanyName, descriptionText, ct);

                db.MatchResults.Add(new MatchResult
                {
                    Id = Guid.NewGuid(),
                    UserId = SeedData.DefaultUserId,
                    JobPostingId = job.Id,
                    CandidateProfileId = activeProfile.Id,
                    VectorScore = distance,
                    LlmScore = score.Score,
                    LlmReasoning = score.Reasoning,
                    Status = MatchStatus.PendingReview,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                });
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One bad LLM call (rate limit, malformed JSON) must not lose the rest of this
                // tick's candidates — log and move on, the unique (UserId, JobPostingId) index
                // means it'll be retried next tick since no MatchResult was created for it.
                logger.LogError(ex, "Scoring job posting {JobPostingId} failed.", job.Id);
            }
        }
    }

    private async Task EmbedMissingJobPostingsAsync(CancellationToken ct)
    {
        var pending = await db.JobPostings
            .Where(p => p.IsActive)
            .Where(p => EF.Property<SqlVector<float>?>(p, AppDbContext.JobEmbeddingColumn) == null)
            .ToListAsync(ct);

        foreach (var chunk in pending.Chunk(options.Value.EmbeddingBatchSize))
        {
            var texts = chunk
                .Select(p => Truncate($"{p.Title} at {p.CompanyName}. {p.DescriptionText}", options.Value.MaxDescriptionChars))
                .ToList();
            var embeddings = await embeddingGenerator.GenerateAsync(texts, cancellationToken: ct);

            for (var i = 0; i < chunk.Length; i++)
            {
                db.Entry(chunk[i]).Property<SqlVector<float>?>(AppDbContext.JobEmbeddingColumn).CurrentValue =
                    new SqlVector<float>(embeddings[i].Vector);
            }

            await db.SaveChangesAsync(ct);
        }
    }

    private async Task ClassifyPostingsAsync(CancellationToken ct)
    {
        var pending = await db.JobPostings
            .Where(p => p.IsActive && p.ClassifiedAtUtc == null)
            .ToListAsync(ct);

        foreach (var chunk in pending.Chunk(options.Value.EmbeddingBatchSize))
        {
            var inputs = chunk
                .Select(p => new JobPostingClassificationInput(
                    p.Title, p.CompanyName, p.LocationText, Truncate(p.DescriptionText ?? "", options.Value.MaxDescriptionChars)))
                .ToList();
            var classifications = await postingClassifier.ClassifyAsync(inputs, ct);

            for (var i = 0; i < chunk.Length; i++)
            {
                chunk[i].VisaSponsorship = classifications[i].VisaSponsorship;
                chunk[i].SalaryMinAnnualUsd = classifications[i].SalaryMinAnnualUsd;
                chunk[i].SalaryMaxAnnualUsd = classifications[i].SalaryMaxAnnualUsd;
                chunk[i].ApplicationDeadline = classifications[i].ApplicationDeadline;
                chunk[i].WorkLocationCountry = classifications[i].WorkLocationCountry;
                chunk[i].ClassifiedAtUtc = DateTimeOffset.UtcNow;
            }

            await db.SaveChangesAsync(ct);
        }
    }

    private async Task<List<(JobPosting Job, double Distance)>> PrefilterCandidatesAsync(
        SqlVector<float> profileEmbedding, bool requiresVisaSponsorship, int? minimumSalaryUsd,
        string? requiredCountry, List<string> excludedCompanies, CancellationToken ct)
    {
        var userId = SeedData.DefaultUserId;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = db.JobPostings
            .Where(p => p.IsActive)
            .Where(p => EF.Property<SqlVector<float>?>(p, AppDbContext.JobEmbeddingColumn) != null)
            .Where(p => !db.MatchResults.Any(m => m.UserId == userId && m.JobPostingId == p.Id))
            // A passed deadline is objectively dead for anyone — not a per-user preference like
            // the filters below, so it's unconditional. Unstated deadlines (the common case) pass.
            .Where(p => p.ApplicationDeadline == null || p.ApplicationDeadline >= today);

        if (requiresVisaSponsorship)
        {
            // Unspecified (posting says nothing) is deliberately NOT excluded — only an explicit
            // NoSponsorship disqualifies. Nulls (not yet classified) also pass here in practice,
            // since ClassifyPostingsAsync just ran to completion above.
            query = query.Where(p => p.VisaSponsorship != VisaSponsorshipStatus.NoSponsorship);
        }

        if (minimumSalaryUsd is { } floor)
        {
            // Only exclude when we HAVE a stated max and it's confidently below the floor —
            // unstated salary (the common case) is never treated as failing the floor.
            query = query.Where(p => p.SalaryMaxAnnualUsd == null || p.SalaryMaxAnnualUsd >= floor);
        }

        if (excludedCompanies.Count > 0)
        {
            query = query.Where(p => !excludedCompanies.Contains(p.CompanyName));
        }

        if (!string.IsNullOrWhiteSpace(requiredCountry))
        {
            // Only exclude when the posting confidently names a DIFFERENT single country —
            // global/ambiguous/unclassified postings (WorkLocationCountry null) still pass.
            query = query.Where(p => p.WorkLocationCountry == null || p.WorkLocationCountry == requiredCountry);
        }

        var results = await query
            .Select(p => new
            {
                Posting = p,
                Distance = EF.Functions.VectorDistance(
                    "cosine",
                    EF.Property<SqlVector<float>>(p, AppDbContext.JobEmbeddingColumn),
                    profileEmbedding),
            })
            .OrderBy(x => x.Distance)
            .Take(options.Value.TopN)
            .ToListAsync(ct);

        return results.Select(x => (x.Posting, x.Distance)).ToList();
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];
}
