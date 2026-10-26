using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VLibrary.Application.Abstractions;
using VLibrary.Application.Common;
using VLibrary.Application.Features.Categories;
using VLibrary.Domain.Common;
using VLibrary.Domain.Enums;
using VLibrary.Domain.Rules;

namespace VLibrary.Application.Features.Books
{
    /// <summary>
    /// UC-4: catalog search and filters.
    /// </summary>
    /// <param name="Q">Free text: title, authors, ISBN, tags and note text.</param>
    /// <param name="OnShelf">"На полках": the book is on at least one shelf.</param>
    public sealed record SearchBooksQuery(
        string? Q = null,
        long? ShelfId = null,
        bool? OnShelf = null,
        ReadingStatus? Status = null,
        string? Tag = null,
        FileTypesEnum? Format = null,
        int? MinRating = null,
        long? AuthorId = null,
        long? CategoryId = null,
        BookSort Sort = BookSort.Title,
        int Page = 1,
        int PageSize = 50) : IRequest<Result<PagedList<BookListItemDto>>>;

    internal sealed class SearchBooksValidator : AbstractValidator<SearchBooksQuery>
    {
        public SearchBooksValidator()
        {
            RuleFor(q => q.Q).MaximumLength(200);
            RuleFor(q => q.MinRating).InclusiveBetween(1, 10);
            RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
            RuleFor(q => q.PageSize).InclusiveBetween(1, 200);
        }
    }

    internal sealed class SearchBooksHandler(IAppDbContext db, ITextSearch textSearch)
        : IRequestHandler<SearchBooksQuery, Result<PagedList<BookListItemDto>>>
    {
        public async Task<Result<PagedList<BookListItemDto>>> Handle(SearchBooksQuery request, CancellationToken cancellationToken)
        {
            var query = db.Books.AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Q))
            {
                query = textSearch.FilterBooks(query, request.Q.Trim());
            }

            if (request.ShelfId.HasValue)
            {
                query = query.Where(b => b.ShelfBooks.Any(sb => sb.BookshelfId == request.ShelfId.Value));
            }

            if (request.OnShelf.HasValue)
            {
                query = request.OnShelf.Value ? query.Where(b => b.ShelfBooks.Any()) : query.Where(b => !b.ShelfBooks.Any());
            }

            if (request.Status.HasValue)
            {
                query = query.Where(b => b.ReadingStatus == request.Status.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Tag))
            {
                var tagKey = TextNormalizer.Normalize(request.Tag);
                query = query.Where(b => b.Tags.Any(t => t.NormalizedName == tagKey));
            }

            if (request.Format.HasValue)
            {
                query = query.Where(b => b.FilesMetas.Any(f => f.Format == request.Format.Value));
            }

            if (request.MinRating.HasValue)
            {
                query = query.Where(b => b.Rating >= request.MinRating.Value);
            }

            if (request.AuthorId.HasValue)
            {
                query = query.Where(b => b.BookAuthors.Any(ba => ba.AuthorId == request.AuthorId.Value));
            }

            if (request.CategoryId.HasValue)
            {
                // The category and its subcategories.
                var categoryIds = await CategoryTree.SubtreeIdsAsync(db, request.CategoryId.Value, cancellationToken);
                query = query.Where(b => b.FilesMetas.Any(f => categoryIds.Contains(f.CategoryId)));
            }

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .ApplySort(request.Sort)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListItemsAsync(cancellationToken);

            return new PagedList<BookListItemDto>(items, request.Page, request.PageSize, total);
        }
    }
}
