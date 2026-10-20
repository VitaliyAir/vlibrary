using MediatR;
using Microsoft.EntityFrameworkCore;
using VLibrary.Application.Abstractions;
using VLibrary.Domain.Common;
using VLibrary.Domain.Rules;

namespace VLibrary.Application.Features.Tags
{
    public sealed record TagDto(long Id, string Name, int BooksCount);

    /// <summary>Tag autocomplete (prefix match) or all tags by popularity.</summary>
    public sealed record GetTagsQuery(string? Q = null, int Take = 30) : IRequest<Result<IReadOnlyList<TagDto>>>;

    internal sealed class GetTagsHandler(IAppDbContext db) : IRequestHandler<GetTagsQuery, Result<IReadOnlyList<TagDto>>>
    {
        public async Task<Result<IReadOnlyList<TagDto>>> Handle(GetTagsQuery request, CancellationToken cancellationToken)
        {
            var query = db.Tags.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(request.Q))
            {
                var key = TextNormalizer.Normalize(request.Q);
                query = query.Where(t => t.NormalizedName.StartsWith(key));
            }

            var tags = await query
                .Select(t => new TagDto(t.Id, t.Name, t.Books.Count(b => !b.IsDeleted)))
                .OrderByDescending(t => t.BooksCount)
                .ThenBy(t => t.Name)
                .Take(Math.Clamp(request.Take, 1, 200))
                .ToListAsync(cancellationToken);

            return tags;
        }
    }
}
