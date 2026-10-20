using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VLibrary.Application.Abstractions;
using VLibrary.Application.Common;
using VLibrary.Domain.Common;
using VLibrary.Domain.Enums;

namespace VLibrary.Application.Features.Imports
{
    /// <summary>Recent import batches, newest first.</summary>
    public sealed record GetImportsQuery(int Take = 20) : IRequest<Result<IReadOnlyList<ImportBatchDto>>>;

    /// <summary>One batch with its progress and a page of per-file results.</summary>
    public sealed record GetImportQuery(long Id, ImportOutcome? Outcome = null, int Page = 1, int PageSize = 100)
        : IRequest<Result<ImportBatchDetailsDto>>;

    internal sealed class GetImportsValidator : AbstractValidator<GetImportsQuery>
    {
        public GetImportsValidator()
        {
            RuleFor(q => q.Take).InclusiveBetween(1, 200);
        }
    }

    internal sealed class GetImportValidator : AbstractValidator<GetImportQuery>
    {
        public GetImportValidator()
        {
            RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
            RuleFor(q => q.PageSize).InclusiveBetween(1, 500);
        }
    }

    internal sealed class GetImportsHandler(IAppDbContext db)
        : IRequestHandler<GetImportsQuery, Result<IReadOnlyList<ImportBatchDto>>>
    {
        public async Task<Result<IReadOnlyList<ImportBatchDto>>> Handle(GetImportsQuery request, CancellationToken cancellationToken)
        {
            var batches = await db.ImportBatches
                .AsNoTracking()
                .OrderByDescending(b => b.Id)
                .Take(request.Take)
                .Select(ImportBatchDto.Projection)
                .ToListAsync(cancellationToken);

            return batches;
        }
    }

    internal sealed class GetImportHandler(IAppDbContext db)
        : IRequestHandler<GetImportQuery, Result<ImportBatchDetailsDto>>
    {
        public async Task<Result<ImportBatchDetailsDto>> Handle(GetImportQuery request, CancellationToken cancellationToken)
        {
            var batch = await db.ImportBatches
                .AsNoTracking()
                .Where(b => b.Id == request.Id)
                .Select(ImportBatchDto.Projection)
                .FirstOrDefaultAsync(cancellationToken);

            if (batch == null)
            {
                return ImportErrors.NotFound(request.Id);
            }

            var itemsQuery = db.ImportItems.AsNoTracking().Where(i => i.ImportBatchId == request.Id);
            if (request.Outcome.HasValue)
            {
                itemsQuery = itemsQuery.Where(i => i.Outcome == request.Outcome.Value);
            }

            var total = await itemsQuery.CountAsync(cancellationToken);
            var items = await itemsQuery
                .OrderBy(i => i.Id)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(i => new ImportItemDto(
                    i.Id, i.Path, i.Outcome, i.Message, i.FileMetaId, i.FileMeta != null ? i.FileMeta.BookId : null))
                .ToListAsync(cancellationToken);

            return new ImportBatchDetailsDto(batch, new PagedList<ImportItemDto>(items, request.Page, request.PageSize, total));
        }
    }
}
