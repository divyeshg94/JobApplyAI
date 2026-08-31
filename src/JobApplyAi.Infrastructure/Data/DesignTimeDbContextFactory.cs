using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobApplyAi.Infrastructure.Data;

/// <summary>
/// Used by `dotnet ef` only. Reads JOBAPPLYAI_SQL when set (needed for `database update`);
/// falls back to a placeholder, which is enough for `migrations add`.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("JOBAPPLYAI_SQL")
            ?? "Server=placeholder;Database=JobApplyAi;";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseAzureSql(connectionString, sql =>
                sql.MigrationsHistoryTable(AppDbContext.MigrationsHistoryTable, AppDbContext.Schema))
            .Options;

        return new AppDbContext(options);
    }
}
