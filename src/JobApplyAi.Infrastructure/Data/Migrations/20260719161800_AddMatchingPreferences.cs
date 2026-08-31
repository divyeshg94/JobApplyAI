using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApplyAi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchingPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SalaryMaxAnnualUsd",
                schema: "jobapply",
                table: "JobPostings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SalaryMinAnnualUsd",
                schema: "jobapply",
                table: "JobPostings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VisaSponsorship",
                schema: "jobapply",
                table: "JobPostings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumSalaryUsd",
                schema: "jobapply",
                table: "CandidateProfiles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresVisaSponsorship",
                schema: "jobapply",
                table: "CandidateProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ProfileExcludedCompanies",
                schema: "jobapply",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileExcludedCompanies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfileExcludedCompanies_CandidateProfiles_CandidateProfileId",
                        column: x => x.CandidateProfileId,
                        principalSchema: "jobapply",
                        principalTable: "CandidateProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProfileExcludedCompanies_CandidateProfileId",
                schema: "jobapply",
                table: "ProfileExcludedCompanies",
                column: "CandidateProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfileExcludedCompanies",
                schema: "jobapply");

            migrationBuilder.DropColumn(
                name: "SalaryMaxAnnualUsd",
                schema: "jobapply",
                table: "JobPostings");

            migrationBuilder.DropColumn(
                name: "SalaryMinAnnualUsd",
                schema: "jobapply",
                table: "JobPostings");

            migrationBuilder.DropColumn(
                name: "VisaSponsorship",
                schema: "jobapply",
                table: "JobPostings");

            migrationBuilder.DropColumn(
                name: "MinimumSalaryUsd",
                schema: "jobapply",
                table: "CandidateProfiles");

            migrationBuilder.DropColumn(
                name: "RequiresVisaSponsorship",
                schema: "jobapply",
                table: "CandidateProfiles");
        }
    }
}
