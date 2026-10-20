using System.Globalization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VLibrary.Application.Abstractions;
using VLibrary.Domain.Common;
using VLibrary.Domain.Entities;

namespace VLibrary.Application.Features.Settings
{
    public sealed record SettingsDto(bool RemindersEnabled, int RemindersPerStartup);

    public sealed record GetSettingsQuery : IRequest<Result<SettingsDto>>;

    public sealed record UpdateSettingsCommand(SettingsDto Settings) : IRequest<Result>;

    internal sealed class UpdateSettingsValidator : AbstractValidator<UpdateSettingsCommand>
    {
        public UpdateSettingsValidator() => RuleFor(c => c.Settings.RemindersPerStartup).InclusiveBetween(1, 10);
    }

    /// <summary>
    /// Typed access to the key/value app_settings table.
    /// </summary>
    internal static class SettingsStore
    {
        private const string RemindersEnabledKey = "reminders.enabled";
        private const string RemindersPerStartupKey = "reminders.perStartup";

        public static readonly SettingsDto Defaults = new(RemindersEnabled: true, RemindersPerStartup: 1);

        public static async Task<SettingsDto> LoadAsync(IAppDbContext db, CancellationToken cancellationToken)
        {
            var values = await db.AppSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

            return new SettingsDto(
                values.TryGetValue(RemindersEnabledKey, out var enabled) && bool.TryParse(enabled, out var e) ? e : Defaults.RemindersEnabled,
                values.TryGetValue(RemindersPerStartupKey, out var count) && int.TryParse(count, CultureInfo.InvariantCulture, out var c)
                    ? c
                    : Defaults.RemindersPerStartup);
        }

        public static async Task SaveAsync(IAppDbContext db, SettingsDto settings, CancellationToken cancellationToken)
        {
            await SetAsync(db, RemindersEnabledKey, settings.RemindersEnabled.ToString(), cancellationToken);
            await SetAsync(db, RemindersPerStartupKey, settings.RemindersPerStartup.ToString(CultureInfo.InvariantCulture), cancellationToken);
        }

        private static async Task SetAsync(IAppDbContext db, string key, string value, CancellationToken cancellationToken)
        {
            var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
            if (setting == null)
            {
                db.AppSettings.Add(new AppSettingEntity { Key = key, Value = value });
            }
            else
            {
                setting.Value = value;
            }
        }
    }

    internal sealed class SettingsHandlers(IAppDbContext db)
        : IRequestHandler<GetSettingsQuery, Result<SettingsDto>>,
          IRequestHandler<UpdateSettingsCommand, Result>
    {
        public async Task<Result<SettingsDto>> Handle(GetSettingsQuery request, CancellationToken cancellationToken) =>
            await SettingsStore.LoadAsync(db, cancellationToken);

        public async Task<Result> Handle(UpdateSettingsCommand request, CancellationToken cancellationToken)
        {
            await SettingsStore.SaveAsync(db, request.Settings, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }
}
