using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VLibrary.Application.Abstractions;
using VLibrary.Application.Common;
using VLibrary.Domain.Common;
using VLibrary.Domain.Entities;
using VLibrary.Domain.Rules;
using VLibrary.Domain.ValueObjects;

namespace VLibrary.Application.Features.Authors
{
    public sealed record AuthorDto(
        long Id, string DisplayName, string? FirstName, string? MiddleName, string? LastName, string? NameInitials, int BooksCount);

    public sealed record AuthorBookDto(long Id, string Title, int? YearPublished);

    public sealed record AuthorDetailsDto(
        long Id, string DisplayName, string? FirstName, string? MiddleName, string? LastName, string? NameInitials,
        string? Biography, IReadOnlyList<AuthorBookDto> Books);

    public sealed record AuthorFields(string? FirstName, string? MiddleName, string? LastName, string? NameInitials, string? Biography);

    public sealed record GetAuthorsQuery(string? Q = null, int Page = 1, int PageSize = 50) : IRequest<Result<PagedList<AuthorDto>>>;

    public sealed record GetAuthorQuery(long Id) : IRequest<Result<AuthorDetailsDto>>;

    public sealed record CreateAuthorCommand(AuthorFields Fields) : IRequest<Result<long>>;

    public sealed record UpdateAuthorCommand(long Id, AuthorFields Fields) : IRequest<Result>;

    public sealed record DeleteAuthorCommand(long Id) : IRequest<Result>;

    internal sealed class AuthorFieldsValidator : AbstractValidator<AuthorFields>
    {
        public AuthorFieldsValidator()
        {
            RuleFor(f => f.LastName).NotEmpty().MaximumLength(150);
            RuleFor(f => f.FirstName).MaximumLength(100);
            RuleFor(f => f.MiddleName).MaximumLength(100);
            RuleFor(f => f.NameInitials).MaximumLength(20);
        }
    }

    internal sealed class CreateAuthorValidator : AbstractValidator<CreateAuthorCommand>
    {
        public CreateAuthorValidator() => RuleFor(c => c.Fields).SetValidator(new AuthorFieldsValidator());
    }

    internal sealed class UpdateAuthorValidator : AbstractValidator<UpdateAuthorCommand>
    {
        public UpdateAuthorValidator() => RuleFor(c => c.Fields).SetValidator(new AuthorFieldsValidator());
    }

    internal sealed class GetAuthorsValidator : AbstractValidator<GetAuthorsQuery>
    {
        public GetAuthorsValidator()
        {
            RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
            RuleFor(q => q.PageSize).InclusiveBetween(1, 200);
        }
    }

    internal sealed class AuthorHandlers(IAppDbContext db)
        : IRequestHandler<GetAuthorsQuery, Result<PagedList<AuthorDto>>>,
          IRequestHandler<GetAuthorQuery, Result<AuthorDetailsDto>>,
          IRequestHandler<CreateAuthorCommand, Result<long>>,
          IRequestHandler<UpdateAuthorCommand, Result>,
          IRequestHandler<DeleteAuthorCommand, Result>
    {
        public async Task<Result<PagedList<AuthorDto>>> Handle(GetAuthorsQuery request, CancellationToken cancellationToken)
        {
            var query = db.Authors.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(request.Q))
            {
                var key = TextNormalizer.Normalize(request.Q);
                query = query.Where(a => a.NormalizedName.Contains(key)
                    || (a.FirstName != null && a.FirstName.ToLower().Contains(key))
                    || (a.LastName != null && a.LastName.ToLower().Contains(key)));
            }

            var total = await query.CountAsync(cancellationToken);
            var rows = await query
                .OrderBy(a => a.LastName).ThenBy(a => a.FirstName).ThenBy(a => a.Id)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(a => new { a.Id, a.FirstName, a.MiddleName, a.LastName, a.NameInitials, BooksCount = a.BookAuthors.Count(ba => !ba.Book.IsDeleted) })
                .ToListAsync(cancellationToken);

            var items = rows
                .Select(a => new AuthorDto(
                    a.Id, PersonName.Format(a.LastName, a.FirstName, a.MiddleName, a.NameInitials),
                    a.FirstName, a.MiddleName, a.LastName, a.NameInitials, a.BooksCount))
                .ToList();
            return new PagedList<AuthorDto>(items, request.Page, request.PageSize, total);
        }

        public async Task<Result<AuthorDetailsDto>> Handle(GetAuthorQuery request, CancellationToken cancellationToken)
        {
            var author = await db.Authors.AsNoTracking()
                .Where(a => a.Id == request.Id)
                .Select(a => new
                {
                    a.Id, a.FirstName, a.MiddleName, a.LastName, a.NameInitials, a.Biography,
                    Books = a.BookAuthors
                        .Where(ba => !ba.Book.IsDeleted)
                        .OrderBy(ba => ba.Book.Title)
                        .Select(ba => new AuthorBookDto(ba.BookId, ba.Book.Title, ba.Book.YearPublished))
                        .ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (author == null)
            {
                return CommonErrors.NotFound("Author", request.Id);
            }

            return new AuthorDetailsDto(
                author.Id, PersonName.Format(author.LastName, author.FirstName, author.MiddleName, author.NameInitials),
                author.FirstName, author.MiddleName, author.LastName, author.NameInitials, author.Biography, author.Books);
        }

        public async Task<Result<long>> Handle(CreateAuthorCommand request, CancellationToken cancellationToken)
        {
            var author = new AuthorInfoEntity();
            Apply(author, request.Fields);
            db.Authors.Add(author);
            await db.SaveChangesAsync(cancellationToken);
            return author.Id;
        }

        public async Task<Result> Handle(UpdateAuthorCommand request, CancellationToken cancellationToken)
        {
            var author = await db.Authors.FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);
            if (author == null)
            {
                return CommonErrors.NotFound("Author", request.Id);
            }

            Apply(author, request.Fields);

            // Author names are part of the books' search text.
            var books = await db.Books
                .Where(b => b.BookAuthors.Any(ba => ba.AuthorId == request.Id))
                .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
                .Include(b => b.Tags)
                .ToListAsync(cancellationToken);
            foreach (var book in books)
            {
                book.RefreshSearchText();
            }

            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        public async Task<Result> Handle(DeleteAuthorCommand request, CancellationToken cancellationToken)
        {
            var author = await db.Authors.FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);
            if (author == null)
            {
                return CommonErrors.NotFound("Author", request.Id);
            }

            if (await db.BookAuthors.AnyAsync(ba => ba.AuthorId == request.Id, cancellationToken))
            {
                return CommonErrors.InUse("Author", request.Id, "the author has books");
            }

            db.Authors.Remove(author);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        private static void Apply(AuthorInfoEntity author, AuthorFields fields)
        {
            author.FirstName = Clean(fields.FirstName);
            author.MiddleName = Clean(fields.MiddleName);
            author.LastName = Clean(fields.LastName);
            author.NameInitials = Clean(fields.NameInitials);
            author.Biography = fields.Biography;
            author.RefreshNormalizedName();
        }

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
