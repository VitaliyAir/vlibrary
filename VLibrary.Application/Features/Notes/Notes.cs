using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VLibrary.Application.Abstractions;
using VLibrary.Application.Common;
using VLibrary.Domain.Common;
using VLibrary.Domain.Entities;
using VLibrary.Domain.Enums;

namespace VLibrary.Application.Features.Notes
{
    public sealed record NoteDto(
        long Id,
        long BookId,
        string BookTitle,
        long? FileMetaId,
        string? FileName,
        NoteType Type,
        string Text,
        int? PageNumber,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        int ReviewBox,
        DateTime NextReviewAt,
        DateTime? LastReviewedAt);

    /// <param name="FileMetaId">The file the page refers to; when omitted and the book has one file, that file is used.</param>
    public sealed record NoteFields(NoteType Type, string Text, int? PageNumber = null, long? FileMetaId = null);

    public sealed record GetBookNotesQuery(long BookId) : IRequest<Result<IReadOnlyList<NoteDto>>>;

    /// <summary>UC-4: search in the text of personal notes.</summary>
    public sealed record SearchNotesQuery(string? Q, int Page = 1, int PageSize = 50) : IRequest<Result<PagedList<NoteDto>>>;

    /// <summary>UC-6: a note or quote with its page number.</summary>
    public sealed record CreateNoteCommand(long BookId, NoteFields Fields) : IRequest<Result<long>>;

    public sealed record UpdateNoteCommand(long Id, NoteFields Fields) : IRequest<Result>;

    public sealed record DeleteNoteCommand(long Id) : IRequest<Result>;

    internal static class NoteProjection
    {
        public static IQueryable<NoteDto> ToDtos(this IQueryable<BookNoteEntity> notes) => notes.Select(n => new NoteDto(
            n.Id, n.BookId, n.Book.Title, n.FileMetaId, n.FileMeta != null ? n.FileMeta.FileName : null,
            n.Type, n.Text, n.PageNumber, n.CreatedAt, n.UpdatedAt, n.ReviewBox, n.NextReviewAt, n.LastReviewedAt));
    }

    internal sealed class NoteFieldsValidator : AbstractValidator<NoteFields>
    {
        public NoteFieldsValidator()
        {
            RuleFor(f => f.Text).NotEmpty().MaximumLength(20_000);
            RuleFor(f => f.PageNumber).GreaterThan(0);
            RuleFor(f => f.Type).IsInEnum();
        }
    }

    internal sealed class CreateNoteValidator : AbstractValidator<CreateNoteCommand>
    {
        public CreateNoteValidator() => RuleFor(c => c.Fields).SetValidator(new NoteFieldsValidator());
    }

    internal sealed class UpdateNoteValidator : AbstractValidator<UpdateNoteCommand>
    {
        public UpdateNoteValidator() => RuleFor(c => c.Fields).SetValidator(new NoteFieldsValidator());
    }

    internal sealed class SearchNotesValidator : AbstractValidator<SearchNotesQuery>
    {
        public SearchNotesValidator()
        {
            RuleFor(q => q.Q).MaximumLength(200);
            RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
            RuleFor(q => q.PageSize).InclusiveBetween(1, 200);
        }
    }

    internal sealed class NoteQueryHandlers(IAppDbContext db, ITextSearch textSearch)
        : IRequestHandler<GetBookNotesQuery, Result<IReadOnlyList<NoteDto>>>,
          IRequestHandler<SearchNotesQuery, Result<PagedList<NoteDto>>>
    {
        public async Task<Result<IReadOnlyList<NoteDto>>> Handle(GetBookNotesQuery request, CancellationToken cancellationToken)
        {
            if (!await db.Books.AnyAsync(b => b.Id == request.BookId, cancellationToken))
            {
                return CommonErrors.NotFound("Book", request.BookId);
            }

            var notes = await db.BookNotes.AsNoTracking()
                .Where(n => n.BookId == request.BookId)
                .OrderBy(n => n.PageNumber == null).ThenBy(n => n.PageNumber).ThenBy(n => n.CreatedAt)
                .ToDtos()
                .ToListAsync(cancellationToken);
            return notes;
        }

        public async Task<Result<PagedList<NoteDto>>> Handle(SearchNotesQuery request, CancellationToken cancellationToken)
        {
            var query = db.BookNotes.AsNoTracking().Where(n => !n.Book.IsDeleted);
            if (!string.IsNullOrWhiteSpace(request.Q))
            {
                query = textSearch.FilterNotes(query, request.Q.Trim());
            }

            var total = await query.CountAsync(cancellationToken);
            var notes = await query
                .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToDtos()
                .ToListAsync(cancellationToken);

            return new PagedList<NoteDto>(notes, request.Page, request.PageSize, total);
        }
    }

    internal sealed class NoteCommandHandlers(IAppDbContext db, IClock clock)
        : IRequestHandler<CreateNoteCommand, Result<long>>,
          IRequestHandler<UpdateNoteCommand, Result>,
          IRequestHandler<DeleteNoteCommand, Result>
    {
        public async Task<Result<long>> Handle(CreateNoteCommand request, CancellationToken cancellationToken)
        {
            var fileIds = await db.FileMetas.Where(f => f.BookId == request.BookId).Select(f => f.Id).ToListAsync(cancellationToken);
            if (!await db.Books.AnyAsync(b => b.Id == request.BookId, cancellationToken))
            {
                return CommonErrors.NotFound("Book", request.BookId);
            }

            var fileId = ResolveFileId(request.Fields.FileMetaId, fileIds);
            if (fileId.IsFailure)
            {
                return fileId.Error;
            }

            var fields = request.Fields;
            var note = BookNoteEntity.Create(request.BookId, fields.Type, fields.Text, fields.PageNumber, fileId.Value, clock.UtcNow);
            if (note.IsFailure)
            {
                return note.Error;
            }

            db.BookNotes.Add(note.Value);
            await db.SaveChangesAsync(cancellationToken);
            return note.Value.Id;
        }

        public async Task<Result> Handle(UpdateNoteCommand request, CancellationToken cancellationToken)
        {
            var note = await db.BookNotes.FirstOrDefaultAsync(n => n.Id == request.Id, cancellationToken);
            if (note == null)
            {
                return CommonErrors.NotFound("Note", request.Id);
            }

            var fileIds = await db.FileMetas.Where(f => f.BookId == note.BookId).Select(f => f.Id).ToListAsync(cancellationToken);
            var fileId = ResolveFileId(request.Fields.FileMetaId ?? note.FileMetaId, fileIds);
            if (fileId.IsFailure)
            {
                return fileId.Error;
            }

            note.Type = request.Fields.Type;
            note.Text = request.Fields.Text.Trim();
            note.PageNumber = request.Fields.PageNumber;
            note.FileMetaId = fileId.Value;
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        public async Task<Result> Handle(DeleteNoteCommand request, CancellationToken cancellationToken)
        {
            var note = await db.BookNotes.FirstOrDefaultAsync(n => n.Id == request.Id, cancellationToken);
            if (note == null)
            {
                return CommonErrors.NotFound("Note", request.Id);
            }

            db.BookNotes.Remove(note);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        private static Result<long?> ResolveFileId(long? requested, IReadOnlyList<long> bookFileIds)
        {
            if (requested.HasValue)
            {
                return bookFileIds.Contains(requested.Value)
                    ? requested
                    : Error.Validation("Note.FileMetaId", $"File {requested} does not belong to this book.");
            }

            return bookFileIds.Count == 1 ? bookFileIds[0] : (long?)null;
        }
    }
}
