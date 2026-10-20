using MediatR;
using Microsoft.EntityFrameworkCore;
using VLibrary.Application.Abstractions;
using VLibrary.Application.Common;
using VLibrary.Application.Features.Notes;
using VLibrary.Application.Features.Settings;
using VLibrary.Domain.Common;
using VLibrary.Domain.Enums;
using VLibrary.Domain.ValueObjects;

namespace VLibrary.Application.Features.Reminders
{
    public sealed record ReminderDto(NoteDto Note, IReadOnlyList<string> Authors);

    /// <summary>
    /// UC-8: notes to show at application start. Due notes first (oldest due date); when none are due,
    /// a random note among the least recently reviewed ones. Empty when reminders are switched off.
    /// </summary>
    public sealed record GetRemindersQuery : IRequest<Result<IReadOnlyList<ReminderDto>>>;

    public sealed record ReviewNoteCommand(long NoteId, ReviewAnswer Answer) : IRequest<Result>;

    internal sealed class ReminderHandlers(IAppDbContext db, IClock clock)
        : IRequestHandler<GetRemindersQuery, Result<IReadOnlyList<ReminderDto>>>,
          IRequestHandler<ReviewNoteCommand, Result>
    {
        private const int RandomPoolSize = 20;

        public async Task<Result<IReadOnlyList<ReminderDto>>> Handle(GetRemindersQuery request, CancellationToken cancellationToken)
        {
            var settings = await SettingsStore.LoadAsync(db, cancellationToken);
            if (!settings.RemindersEnabled)
            {
                return Array.Empty<ReminderDto>();
            }

            var count = settings.RemindersPerStartup;
            var now = clock.UtcNow;

            var noteIds = await db.BookNotes
                .Where(n => !n.Book.IsDeleted && n.NextReviewAt <= now)
                .OrderBy(n => n.NextReviewAt)
                .Select(n => n.Id)
                .Take(count)
                .ToListAsync(cancellationToken);

            if (noteIds.Count < count)
            {
                var pool = await db.BookNotes
                    .Where(n => !n.Book.IsDeleted && !noteIds.Contains(n.Id))
                    .OrderBy(n => n.LastReviewedAt.HasValue).ThenBy(n => n.LastReviewedAt).ThenBy(n => n.CreatedAt)
                    .Select(n => n.Id)
                    .Take(RandomPoolSize)
                    .ToListAsync(cancellationToken);
                noteIds.AddRange(pool.OrderBy(_ => Random.Shared.Next()).Take(count - noteIds.Count));
            }

            if (noteIds.Count == 0)
            {
                return Array.Empty<ReminderDto>();
            }

            var notes = await db.BookNotes.AsNoTracking().Where(n => noteIds.Contains(n.Id)).ToDtos().ToListAsync(cancellationToken);
            var bookIds = notes.Select(n => n.BookId).Distinct().ToList();
            var authors = await db.BookAuthors.AsNoTracking()
                .Where(ba => bookIds.Contains(ba.BookId))
                .OrderBy(ba => ba.Order)
                .Select(ba => new { ba.BookId, ba.Author.LastName, ba.Author.FirstName, ba.Author.MiddleName, ba.Author.NameInitials })
                .ToListAsync(cancellationToken);
            var authorsByBook = authors.ToLookup(
                a => a.BookId, a => PersonName.Format(a.LastName, a.FirstName, a.MiddleName, a.NameInitials));

            return noteIds
                .Select(id => notes.FirstOrDefault(n => n.Id == id))
                .OfType<NoteDto>()
                .Select(n => new ReminderDto(n, authorsByBook[n.BookId].ToList()))
                .ToList();
        }

        public async Task<Result> Handle(ReviewNoteCommand request, CancellationToken cancellationToken)
        {
            var note = await db.BookNotes.FirstOrDefaultAsync(n => n.Id == request.NoteId, cancellationToken);
            if (note == null)
            {
                return CommonErrors.NotFound("Note", request.NoteId);
            }

            if (!Enum.IsDefined(request.Answer))
            {
                return Error.Validation("Reminder.Answer", "Answer must be Remember, Forgot or Skip.");
            }

            note.Review(request.Answer, clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }
}
