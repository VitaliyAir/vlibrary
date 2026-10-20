using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace VLibrary.Infrastructure.Persistence
{
    internal static class AppDbContextOptions
    {
        public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder options, string connectionString) =>
            options
                .UseNpgsql(connectionString, npgsql => npgsql
                    .MigrationsHistoryTable("__ef_migrations_history")
                    // Queries loading several collections (authors, tags, files) run as one query per collection
                    // instead of one JOIN whose rows multiply. All such queries have a deterministic order.
                    .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
                .UseSnakeCaseNamingConvention()
                // Soft-deleted books/files are hidden by query filters on purpose; dependent rows go with them.
                .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
    }

    /// <summary>
    /// Used by "dotnet ef migrations add"; no database connection is needed for that.
    /// </summary>
    internal sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__VLibraryDb")
                ?? "Host=localhost;Port=5432;Database=vlibrarydb;Username=postgres";

            var options = new DbContextOptionsBuilder<AppDbContext>();
            AppDbContextOptions.Configure(options, connectionString);
            return new AppDbContext(options.Options);
        }
    }
}
