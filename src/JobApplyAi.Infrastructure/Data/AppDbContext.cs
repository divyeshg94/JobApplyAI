using JobApplyAi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobApplyAi.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Dimensions of the embedding model output. Must match the deployed Foundry embedding model
    /// (e.g. text-embedding-3-small = 1536). Changing this requires a migration.
    /// </summary>
    public const int EmbeddingDimensions = 1536;

    /// <summary>
    /// Vector columns are shadow properties so Domain stays free of provider types
    /// (SqlVector&lt;float&gt; lives in Microsoft.Data.SqlClient). Access via
    /// EF.Property&lt;SqlVector&lt;float&gt;?&gt;(entity, name) in queries.
    /// </summary>
    public const string ProfileEmbeddingColumn = "ProfileEmbedding";
    public const string JobEmbeddingColumn = "JobEmbedding";

    public DbSet<User> Users => Set<User>();
    public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();
    public DbSet<ProfileWorkExperience> ProfileWorkExperiences => Set<ProfileWorkExperience>();
    public DbSet<ProfileEducation> ProfileEducations => Set<ProfileEducation>();
    public DbSet<ProfileSkill> ProfileSkills => Set<ProfileSkill>();
    public DbSet<ProfileExcludedCompany> ProfileExcludedCompanies => Set<ProfileExcludedCompany>();
    public DbSet<JobSourceSubscription> JobSourceSubscriptions => Set<JobSourceSubscription>();
    public DbSet<JobPosting> JobPostings => Set<JobPosting>();
    public DbSet<MatchResult> MatchResults => Set<MatchResult>();
    public DbSet<Domain.Entities.Application> Applications => Set<Domain.Entities.Application>();
    public DbSet<PollRunLog> PollRunLogs => Set<PollRunLog>();

    /// <summary>
    /// Everything lives in this schema (incl. the migrations-history table) because the DB is
    /// cohosted with an unrelated app — no dbo collisions, and `DROP SCHEMA jobapply` is the
    /// clean exit path if this ever moves to its own database.
    /// </summary>
    public const string Schema = "jobapply";
    public const string MigrationsHistoryTable = "__EFMigrationsHistory_JobApply";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
