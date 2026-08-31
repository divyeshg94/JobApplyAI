using System;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApplyAi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "jobapply");

            migrationBuilder.CreateTable(
                name: "JobPostings",
                schema: "jobapply",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    ExternalJobId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LocationText = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DescriptionText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApplyUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PostedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FetchedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RawJsonPayload = table.Column<string>(type: "json", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    JobEmbedding = table.Column<SqlVector<float>>(type: "vector(1536)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobPostings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "jobapply",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CandidateProfiles",
                schema: "jobapply",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RawResumeBlobUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RawResumeFileName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RawResumeContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LocationText = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    LinkedInUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PortfolioUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SummaryText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ParsedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ProfileEmbedding = table.Column<SqlVector<float>>(type: "vector(1536)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "jobapply",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobSourceSubscriptions",
                schema: "jobapply",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    ConfigJson = table.Column<string>(type: "json", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LastPolledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastPollStatus = table.Column<int>(type: "int", nullable: true),
                    LastPollError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobSourceSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobSourceSubscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "jobapply",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatchResults",
                schema: "jobapply",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobPostingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VectorScore = table.Column<double>(type: "float", nullable: false),
                    LlmScore = table.Column<double>(type: "float", nullable: false),
                    LlmReasoning = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NotifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchResults_CandidateProfiles_CandidateProfileId",
                        column: x => x.CandidateProfileId,
                        principalSchema: "jobapply",
                        principalTable: "CandidateProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchResults_JobPostings_JobPostingId",
                        column: x => x.JobPostingId,
                        principalSchema: "jobapply",
                        principalTable: "JobPostings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchResults_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "jobapply",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProfileEducations",
                schema: "jobapply",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Institution = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Degree = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FieldOfStudy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileEducations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfileEducations_CandidateProfiles_CandidateProfileId",
                        column: x => x.CandidateProfileId,
                        principalSchema: "jobapply",
                        principalTable: "CandidateProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfileSkills",
                schema: "jobapply",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfileSkills_CandidateProfiles_CandidateProfileId",
                        column: x => x.CandidateProfileId,
                        principalSchema: "jobapply",
                        principalTable: "CandidateProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfileWorkExperiences",
                schema: "jobapply",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Company = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LocationText = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    DescriptionText = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileWorkExperiences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfileWorkExperiences_CandidateProfiles_CandidateProfileId",
                        column: x => x.CandidateProfileId,
                        principalSchema: "jobapply",
                        principalTable: "CandidateProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PollRunLogs",
                schema: "jobapply",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobSourceSubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    JobsFetched = table.Column<int>(type: "int", nullable: false),
                    JobsNew = table.Column<int>(type: "int", nullable: false),
                    JobsFailed = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollRunLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PollRunLogs_JobSourceSubscriptions_JobSourceSubscriptionId",
                        column: x => x.JobSourceSubscriptionId,
                        principalSchema: "jobapply",
                        principalTable: "JobSourceSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Applications",
                schema: "jobapply",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TailoredResumeBlobUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TailoredCoverLetterBlobUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    GeneratedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AppliedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Applications_MatchResults_MatchResultId",
                        column: x => x.MatchResultId,
                        principalSchema: "jobapply",
                        principalTable: "MatchResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Applications_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "jobapply",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "jobapply",
                table: "Users",
                columns: new[] { "Id", "CreatedAtUtc", "DisplayName", "Email" },
                values: new object[] { new Guid("a1e0c8f0-0000-4000-8000-000000000001"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Default User", "owner@localhost" });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_MatchResultId",
                schema: "jobapply",
                table: "Applications",
                column: "MatchResultId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_UserId_Status",
                schema: "jobapply",
                table: "Applications",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateProfiles_UserId_Status",
                schema: "jobapply",
                table: "CandidateProfiles",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_JobPostings_Source_ExternalJobId",
                schema: "jobapply",
                table: "JobPostings",
                columns: new[] { "Source", "ExternalJobId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobSourceSubscriptions_UserId_IsEnabled",
                schema: "jobapply",
                table: "JobSourceSubscriptions",
                columns: new[] { "UserId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchResults_CandidateProfileId",
                schema: "jobapply",
                table: "MatchResults",
                column: "CandidateProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchResults_JobPostingId",
                schema: "jobapply",
                table: "MatchResults",
                column: "JobPostingId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchResults_UserId_JobPostingId",
                schema: "jobapply",
                table: "MatchResults",
                columns: new[] { "UserId", "JobPostingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchResults_UserId_Status",
                schema: "jobapply",
                table: "MatchResults",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PollRunLogs_JobSourceSubscriptionId",
                schema: "jobapply",
                table: "PollRunLogs",
                column: "JobSourceSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_PollRunLogs_StartedAtUtc",
                schema: "jobapply",
                table: "PollRunLogs",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileEducations_CandidateProfileId",
                schema: "jobapply",
                table: "ProfileEducations",
                column: "CandidateProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileSkills_CandidateProfileId",
                schema: "jobapply",
                table: "ProfileSkills",
                column: "CandidateProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileWorkExperiences_CandidateProfileId",
                schema: "jobapply",
                table: "ProfileWorkExperiences",
                column: "CandidateProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Applications",
                schema: "jobapply");

            migrationBuilder.DropTable(
                name: "PollRunLogs",
                schema: "jobapply");

            migrationBuilder.DropTable(
                name: "ProfileEducations",
                schema: "jobapply");

            migrationBuilder.DropTable(
                name: "ProfileSkills",
                schema: "jobapply");

            migrationBuilder.DropTable(
                name: "ProfileWorkExperiences",
                schema: "jobapply");

            migrationBuilder.DropTable(
                name: "MatchResults",
                schema: "jobapply");

            migrationBuilder.DropTable(
                name: "JobSourceSubscriptions",
                schema: "jobapply");

            migrationBuilder.DropTable(
                name: "CandidateProfiles",
                schema: "jobapply");

            migrationBuilder.DropTable(
                name: "JobPostings",
                schema: "jobapply");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "jobapply");
        }
    }
}
