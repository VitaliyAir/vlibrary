using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VLibrary.Application.Abstractions;
using VLibrary.Application.Common;
using VLibrary.Domain.Common;
using VLibrary.Domain.Entities;
using VLibrary.Domain.Enums;

namespace VLibrary.Application.Features.Books
{
    /// <summary>Editable book card fields (UC-3). Authors are names ("Толстой Л.Н.", "Leo Tolstoy"), matched or created.</summary>
    public sealed record BookFields(
        string Title,
        string? Subtitle = null,
        string? Description = null,
        string? Isbn = null,
        string? Edition = null,
        string? Publisher = null,
        int? YearPublished = null,
        int? TotalPages = null,
        LanguagesEnum? Language = null,
        string? TableOfContents = null,
        string? Glossary = null,
        long? ClassifierId = null,
        IReadOnlyList<string>? Authors = null,
        IReadOnlyList<string>? Tags = null);

    public sealed record CreateBookCommand(BookFields Fields) : IRequest<Result<long>>;

    public sealed record UpdateBookCommand(long Id, BookFields Fields) : IRequest<Result>;

    /// <summary>
    /// The book and its files leave the catalog (kept as tombstones); its notes and shelf links are deleted.
    /// Files stay on disk.
    /// </summary>
    public sealed record DeleteBookCommand(long Id) : IRequest<Result>;

    public sealed record SetBookRatingCommand(long Id, int? Rating) : IRequest<Result>;

    public sealed record SetBookStatusCommand(long Id, ReadingStatus Status) : IRequest<Result>;

    public sealed record SetBookTagsCommand(long Id, IReadOnlyList<string> Tags) : IRequest<Result>;

    internal sealed class BookFieldsValidator : AbstractValidator<BookFields>
    {
        public BookFieldsValidator()
        {
            RuleFor(f => f.Title).NotEmpty().MaximumLength(500);
            RuleFor(f => f.Subtitle).MaximumLength(500);
            RuleFor(f => f.Edition).MaximumLength(100);
            RuleFor(f => f.Publisher).MaximumLength(250);
            RuleFor(f => f.YearPublished).InclusiveBetween(1000, 2100);
            RuleFor(f => f.TotalPages).GreaterThan(0);
            RuleForEach(f => f.Authors).NotEmpty().MaximumLength(300);
            RuleForEach(f => f.Tags).NotEmpty().MaximumLength(100);
        }
    }

    internal sealed class CreateBookValidator : AbstractValidator<CreateBookCommand>
    {
        public CreateBookValidator() => RuleFor(c => c.Fields).SetValidator(new BookFieldsValidator());
    }

    internal sealed class UpdateBookValidator : AbstractValidator<UpdateBookCommand>
    {
        public UpdateBookValidator() => RuleFor(c => c.Fields).SetValidator(new BookFieldsValidator());
    }

    internal sealed class SetBookTagsValidator : AbstractValidator<SetBookTagsCommand>
    {
        public SetBookTagsValidator() => RuleForEach(c => c.Tags).NotEmpty().MaximumLength(100);
    }

    internal sealed class BookCommandHandlers(IAppDbContext db, CatalogResolvers resolvers)
        : IRequestHandler<CreateBookCommand, Result<long>>,
          IRequestHandler<UpdateBookCommand, Result>,
          IRequestHandler<DeleteBookCommand, Result>,
          IRequestHandler<SetBookRatingCommand, Result>,
          IRequestHandler<SetBookStatusCommand, Result>,
          IRequestHandler<SetBookTagsCommand, Result>
    {
        public async Task<Result<long>> Handle(CreateBookCommand request, CancellationToken cancellationToken)
        {
            var book = new BookEntity();
            var applied = await ApplyFieldsAsync(book, request.Fields, cancellationToken);
            if (applied.IsFailure)
            {
                return applied.Error;
            }

            db.Books.Add(book);
            await db.SaveChangesAsync(cancellationToken);
            return book.Id;
        }

        public async Task<Result> Handle(UpdateBookCommand request, CancellationToken cancellationToken)
        {
            var book = await LoadForEditAsync(request.Id, cancellationToken);
            if (book == null)
            {
                return CommonErrors.NotFound("Book", request.Id);
            }

            var applied = await ApplyFieldsAsync(book, request.Fields, cancellationToken);
            if (applied.IsFailure)
            {
                return applied;
            }

            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        public async Task<Result> Handle(DeleteBookCommand request, CancellationToken cancellationToken)
        {
            var book = await db.Books.Include(b => b.FilesMetas).FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);
            if (book == null)
            {
                return CommonErrors.NotFound("Book", request.Id);
            }

            // The book row stays as a tombstone (later imports report the same content as "deleted book");
            // its files become tombstones too. Notes, shelf links and open duplicate candidates are deleted.
            book.IsDeleted = true;
            foreach (var file in book.FilesMetas)
            {
                file.IsDeleted = true;
            }

            db.BookNotes.RemoveRange(await db.BookNotes.Where(n => n.BookId == book.Id).ToListAsync(cancellationToken));
            db.BookshelfBooks.RemoveRange(await db.BookshelfBooks.Where(sb => sb.BookId == book.Id).ToListAsync(cancellationToken));
            db.DuplicateCandidates.RemoveRange(await db.DuplicateCandidates
                .Where(c => c.CandidateBookId == book.Id && c.ResolvedAt == null)
                .ToListAsync(cancellationToken));

            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        public async Task<Result> Handle(SetBookRatingCommand request, CancellationToken cancellationToken)
        {
            var book = await db.Books.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);
            if (book == null)
            {
                return CommonErrors.NotFound("Book", request.Id);
            }

            var result = book.SetRating(request.Rating);
            if (result.IsSuccess)
            {
                await db.SaveChangesAsync(cancellationToken);
            }

            return result;
        }

        public async Task<Result> Handle(SetBookStatusCommand request, CancellationToken cancellationToken)
        {
            var book = await db.Books.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);
            if (book == null)
            {
                return CommonErrors.NotFound("Book", request.Id);
            }

            book.ReadingStatus = request.Status;
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        public async Task<Result> Handle(SetBookTagsCommand request, CancellationToken cancellationToken)
        {
            var book = await LoadForEditAsync(request.Id, cancellationToken);
            if (book == null)
            {
                return CommonErrors.NotFound("Book", request.Id);
            }

            book.SetTags(await resolvers.ResolveTagsAsync(request.Tags, cancellationToken));
            book.RefreshSearchText();
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        private Task<BookEntity?> LoadForEditAsync(long id, CancellationToken cancellationToken) =>
            db.Books
                .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
                .Include(b => b.Tags)
                .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        private async Task<Result> ApplyFieldsAsync(BookEntity book, BookFields fields, CancellationToken cancellationToken)
        {
            var isbn = book.SetIsbn(fields.Isbn);
            if (isbn.IsFailure)
            {
                return isbn;
            }

            if (fields.ClassifierId.HasValue
                && !await db.BookClasificators.AnyAsync(c => c.Id == fields.ClassifierId.Value, cancellationToken))
            {
                return CommonErrors.NotFound("Classifier", fields.ClassifierId.Value);
            }

            book.Title = fields.Title.Trim();
            book.Subtitle = fields.Subtitle;
            book.Description = fields.Description;
            book.Edition = fields.Edition;
            book.Publisher = fields.Publisher;
            book.YearPublished = fields.YearPublished;
            book.TotalPages = fields.TotalPages;
            book.Language = fields.Language;
            book.TableOfContents = fields.TableOfContents;
            book.Glossary = fields.Glossary;
            book.BookClasificatorId = fields.ClassifierId;

            if (fields.Authors != null)
            {
                book.SetAuthors(await resolvers.ResolveAuthorsAsync(fields.Authors, cancellationToken));
            }

            if (fields.Tags != null)
            {
                book.SetTags(await resolvers.ResolveTagsAsync(fields.Tags, cancellationToken));
            }

            book.RefreshSearchText();
            return Result.Success();
        }
    }
}
