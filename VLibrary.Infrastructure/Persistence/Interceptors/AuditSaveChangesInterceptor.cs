using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using VLibrary.Application.Abstractions;
using VLibrary.Domain.Entities;

namespace VLibrary.Infrastructure.Persistence.Interceptors
{
    /// <summary>
    /// Fills CreatedAt/UpdatedAt and writes an <see cref="ActionLog"/> row (JSON changes) for every audited change.
    /// ActionById is the user name from the access token, or "system" for background work.
    /// </summary>
    internal sealed class AuditSaveChangesInterceptor(IClock clock, ICurrentUser currentUser) : SaveChangesInterceptor
    {
        private const string SystemUser = "system";

        // Import bookkeeping is logged in import_items already; cover images are derived data (and binary).
        private static readonly HashSet<Type> NotAudited =
            [typeof(ActionLog), typeof(ImportBatchEntity), typeof(ImportItemEntity), typeof(FileCoverEntity)];

        // A rescan touches these on every file; such updates alone are not worth an audit row.
        private static readonly HashSet<string> HousekeepingProperties =
            [nameof(AuditableEntity.UpdatedAt), nameof(FileMetaEntity.LastSeenAt), nameof(FileMetaEntity.Status),
             nameof(FileMetaEntity.DeletedReportedAt), nameof(FileMetaEntity.CoverCheckedAt)];

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Apply(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Apply(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void Apply(DbContext? context)
        {
            if (context == null)
            {
                return;
            }

            context.ChangeTracker.DetectChanges();
            var now = clock.UtcNow;

            foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Property(e => e.CreatedAt).CurrentValue = now;
                    entry.Property(e => e.UpdatedAt).CurrentValue = now;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Property(e => e.UpdatedAt).CurrentValue = now;
                }
            }

            var logs = context.ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Where(e => !NotAudited.Contains(e.Entity.GetType()) && !e.Metadata.IsOwned())
                .Select(e => BuildLog(e, now, currentUser.UserName ?? SystemUser))
                .OfType<ActionLog>()
                .ToList();

            context.Set<ActionLog>().AddRange(logs);
        }

        private static ActionLog? BuildLog(EntityEntry entry, DateTime now, string actionBy)
        {
            var changes = new Dictionary<string, object?>();
            var properties = entry.Properties.Where(p => p.Metadata.GetComputedColumnSql() == null);

            switch (entry.State)
            {
                case EntityState.Added:
                    foreach (var property in properties)
                    {
                        changes[property.Metadata.Name] = Printable(property.CurrentValue);
                    }
                    break;

                case EntityState.Modified:
                    var modified = properties.Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue)).ToList();
                    if (modified.All(p => HousekeepingProperties.Contains(p.Metadata.Name)))
                    {
                        return null;
                    }

                    foreach (var property in modified)
                    {
                        changes[property.Metadata.Name] = new { From = Printable(property.OriginalValue), To = Printable(property.CurrentValue) };
                    }
                    break;

                case EntityState.Deleted:
                    foreach (var property in properties)
                    {
                        changes[property.Metadata.Name] = Printable(property.OriginalValue);
                    }
                    break;
            }

            return new ActionLog
            {
                ActionById = actionBy,
                EntityName = entry.Metadata.ClrType.Name,
                EntityKey = entry.State == EntityState.Added ? null : BuildKey(entry),
                Action = entry.State.ToString(),
                Timestamp = now,
                Changes = JsonSerializer.Serialize(changes)
            };
        }

        private static string? BuildKey(EntityEntry entry)
        {
            var key = entry.Metadata.FindPrimaryKey();
            return key == null
                ? null
                : string.Join(",", key.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString()));
        }

        private static object? Printable(object? value) => value switch
        {
            byte[] bytes => $"<{bytes.Length} bytes>",
            Enum e => e.ToString(),
            _ => value
        };
    }
}
