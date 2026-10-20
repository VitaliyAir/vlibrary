using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VLibrary.Application.Abstractions;
using VLibrary.Domain.Enums;
using VLibrary.Infrastructure.Files;

namespace VLibrary.Infrastructure.Import
{
    /// <summary>
    /// On start: imports still "Running" were left by a browser that stopped sending (tab closed, API restarted)
    /// and become Cancelled ("interrupted"). Temp folders of unfinished uploads are removed.
    /// </summary>
    internal sealed class InterruptedImportsService(
        IServiceScopeFactory scopeFactory,
        IOptions<ImportOptions> options,
        IClock clock,
        ILogger<InterruptedImportsService> logger) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
                var running = await db.ImportBatches.Where(b => b.Status == ImportStatus.Running).ToListAsync(cancellationToken);
                foreach (var batch in running)
                {
                    batch.Interrupt(clock.UtcNow);
                }

                await db.SaveChangesAsync(cancellationToken);
                if (running.Count > 0)
                {
                    logger.LogInformation("Marked {Count} unfinished import(s) as interrupted", running.Count);
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Before the first migration the table may not exist yet; imports still work.
                logger.LogError(ex, "Cannot mark unfinished imports as interrupted");
            }

            CleanTempFolder();
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private void CleanTempFolder()
        {
            var folder = options.Value.ResolvedTempFolder;
            if (!Directory.Exists(folder))
            {
                return;
            }

            foreach (var leftover in Directory.EnumerateDirectories(folder))
            {
                try
                {
                    Directory.Delete(leftover, recursive: true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning(ex, "Cannot delete the import temp folder {Folder}", leftover);
                }
            }
        }
    }
}
