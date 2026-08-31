using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApplyAi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkLocationCountry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WorkLocationCountry",
                schema: "jobapply",
                table: "JobPostings",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequiredCountry",
                schema: "jobapply",
                table: "CandidateProfiles",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WorkLocationCountry",
                schema: "jobapply",
                table: "JobPostings");

            migrationBuilder.DropColumn(
                name: "RequiredCountry",
                schema: "jobapply",
                table: "CandidateProfiles");
        }
    }
}
