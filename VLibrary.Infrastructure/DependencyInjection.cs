using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using VLibrary.Application.Abstractions;
using VLibrary.Infrastructure.Covers;
using VLibrary.Infrastructure.Files;
using VLibrary.Infrastructure.Import;
using VLibrary.Infrastructure.Metadata;
using VLibrary.Infrastructure.Persistence;
using VLibrary.Infrastructure.Persistence.Interceptors;
using VLibrary.Infrastructure.Storage;

namespace VLibrary.Infrastructure
{
    public static class DependencyInjection
    {
        public const string ConnectionStringName = "VLibraryDb";

        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IClock, SystemClock>();
            // Default "no user" (background work, tools); the API registers the access-token user over it.
            services.TryAddScoped<ICurrentUser, NoCurrentUser>();

            services.AddScoped<AuditSaveChangesInterceptor>();
            services.AddDbContext<AppDbContext>((serviceProvider, options) =>
            {
                var connectionString = configuration.GetConnectionString(ConnectionStringName)
                    ?? throw new InvalidOperationException(
                        $"Connection string '{ConnectionStringName}' is not configured. " +
                        "Set it with 'dotnet user-secrets' or the ConnectionStrings__VLibraryDb environment variable.");

                AppDbContextOptions.Configure(options, connectionString);
                options.AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>());
            });
            services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
            services.AddScoped<IBookCandidateSource, BookCandidateSource>();
            services.AddSingleton<ITextSearch, PostgresTextSearch>();

            services.Configure<ImportOptions>(configuration.GetSection(ImportOptions.SectionName));
            services.Configure<MetadataOptions>(configuration.GetSection(MetadataOptions.SectionName));

            services.AddSingleton<IUploadStaging, UploadStaging>();
            services.AddSingleton<IFormatMetadataExtractor, PdfMetadataExtractor>();
            services.AddSingleton<IFormatMetadataExtractor, EpubMetadataExtractor>();
            services.AddSingleton<IFormatMetadataExtractor, DjvuMetadataExtractor>();
            services.AddSingleton<IBookMetadataReader, BookMetadataReader>();

            services.Configure<CoverOptions>(configuration.GetSection(CoverOptions.SectionName));
            services.AddSingleton<ICoverRenderer, CoverRenderer>();

            services.AddHostedService<InterruptedImportsService>();

            services.AddVStorage(configuration);

            services.AddHealthChecks()
                .AddDbContextCheck<AppDbContext>("database")
                .AddCheck<PendingMigrationsHealthCheck>("migrations", HealthStatus.Unhealthy);

            return services;
        }

        public static async Task ApplyMigrationsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pending.Count > 0)
            {
                logger.LogInformation("Applying migrations: {Migrations}", string.Join(", ", pending));
                await db.Database.MigrateAsync(cancellationToken);
            }
        }
    }

    internal sealed class NoCurrentUser : ICurrentUser
    {
        public string? UserName => null;
        public string? UserId => null;
        public string? AccessToken => null;
    }

    internal sealed class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>
    /// Unhealthy while the database is behind the code (idea kept from the old DbCheck).
    /// </summary>
    internal sealed class PendingMigrationsHealthCheck(AppDbContext db) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            return pending.Count == 0
                ? HealthCheckResult.Healthy("All migrations are applied.")
                : new HealthCheckResult(context.Registration.FailureStatus, "Pending migrations: " + string.Join(", ", pending));
        }
    }
}
