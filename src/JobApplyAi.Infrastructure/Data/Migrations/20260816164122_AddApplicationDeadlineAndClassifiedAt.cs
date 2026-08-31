using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApplyAi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationDeadlineAndClassifiedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ApplicationDeadline",
                schema: "jobapply",
                table: "JobPostings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClassifiedAtUtc",
                schema: "jobapply",
                table: "JobPostings",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicationDeadline",
                schema: "jobapply",
                table: "JobPostings");

            migrationBuilder.DropColumn(
                name: "ClassifiedAtUtc",
                schema: "jobapply",
                table: "JobPostings");
        }
    }
}
