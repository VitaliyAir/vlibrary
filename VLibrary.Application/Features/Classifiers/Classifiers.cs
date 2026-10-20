using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VLibrary.Application.Abstractions;
using VLibrary.Application.Common;
using VLibrary.Domain.Common;
using VLibrary.Domain.Entities;

namespace VLibrary.Application.Features.Classifiers
{
    /// <summary>
    /// Classification tree (About.md): Математика => Геометрия => Задачник ...
    /// </summary>
    public sealed record ClassifierNodeDto(long Id, string Title, long? ParentId, int BooksCount, IReadOnlyList<ClassifierNodeDto> Children);

    public sealed record GetClassifierTreeQuery : IRequest<Result<IReadOnlyList<ClassifierNodeDto>>>;

    public sealed record CreateClassifierCommand(string Title, long? ParentId) : IRequest<Result<long>>;

    public sealed record UpdateClassifierCommand(long Id, string Title, long? ParentId) : IRequest<Result>;

    public sealed record DeleteClassifierCommand(long Id) : IRequest<Result>;

    internal sealed class CreateClassifierValidator : AbstractValidator<CreateClassifierCommand>
    {
        public CreateClassifierValidator() => RuleFor(c => c.Title).NotEmpty().MaximumLength(250);
    }

    internal sealed class UpdateClassifierValidator : AbstractValidator<UpdateClassifierCommand>
    {
        public UpdateClassifierValidator() => RuleFor(c => c.Title).NotEmpty().MaximumLength(250);
    }

    internal static class ClassifierTree
    {
        /// <summary>The classifier and all its descendants (the table is small, so it is loaded whole).</summary>
        public static async Task<List<long>> SubtreeIdsAsync(IAppDbContext db, long rootId, CancellationToken cancellationToken)
        {
            var parents = await db.BookClasificators.AsNoTracking()
                .Select(c => new { c.Id, c.ParentId })
                .ToListAsync(cancellationToken);
            var childrenOf = parents.Where(c => c.ParentId.HasValue).ToLookup(c => c.ParentId!.Value, c => c.Id);

            var result = new List<long>();
            var pending = new Stack<long>([rootId]);
            while (pending.Count > 0)
            {
                var id = pending.Pop();
                if (result.Contains(id))
                {
                    continue;
                }

                result.Add(id);
                foreach (var child in childrenOf[id])
                {
                    pending.Push(child);
                }
            }

            return result;
        }
    }

    internal sealed class ClassifierHandlers(IAppDbContext db)
        : IRequestHandler<GetClassifierTreeQuery, Result<IReadOnlyList<ClassifierNodeDto>>>,
          IRequestHandler<CreateClassifierCommand, Result<long>>,
          IRequestHandler<UpdateClassifierCommand, Result>,
          IRequestHandler<DeleteClassifierCommand, Result>
    {
        public async Task<Result<IReadOnlyList<ClassifierNodeDto>>> Handle(GetClassifierTreeQuery request, CancellationToken cancellationToken)
        {
            var rows = await db.BookClasificators.AsNoTracking()
                .OrderBy(c => c.Title)
                .Select(c => new { c.Id, c.Title, c.ParentId, BooksCount = c.Books.Count })
                .ToListAsync(cancellationToken);
            var childrenOf = rows.ToLookup(r => r.ParentId);

            List<ClassifierNodeDto> Build(long? parentId) => childrenOf[parentId]
                .Select(r => new ClassifierNodeDto(r.Id, r.Title, r.ParentId, r.BooksCount, Build(r.Id)))
                .ToList();

            return Build(null);
        }

        public async Task<Result<long>> Handle(CreateClassifierCommand request, CancellationToken cancellationToken)
        {
            if (request.ParentId.HasValue && !await db.BookClasificators.AnyAsync(c => c.Id == request.ParentId, cancellationToken))
            {
                return CommonErrors.NotFound("Classifier", request.ParentId.Value);
            }

            var classifier = new BookClasificatorEntity { Title = request.Title.Trim(), ParentId = request.ParentId };
            db.BookClasificators.Add(classifier);
            await db.SaveChangesAsync(cancellationToken);
            return classifier.Id;
        }

        public async Task<Result> Handle(UpdateClassifierCommand request, CancellationToken cancellationToken)
        {
            var classifier = await db.BookClasificators.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
            if (classifier == null)
            {
                return CommonErrors.NotFound("Classifier", request.Id);
            }

            if (request.ParentId.HasValue)
            {
                if (!await db.BookClasificators.AnyAsync(c => c.Id == request.ParentId, cancellationToken))
                {
                    return CommonErrors.NotFound("Classifier", request.ParentId.Value);
                }

                var subtree = await ClassifierTree.SubtreeIdsAsync(db, request.Id, cancellationToken);
                if (subtree.Contains(request.ParentId.Value))
                {
                    return Error.Validation("Classifier.Cycle", "A classifier cannot be moved under itself or its descendants.");
                }
            }

            classifier.Title = request.Title.Trim();
            classifier.ParentId = request.ParentId;
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        public async Task<Result> Handle(DeleteClassifierCommand request, CancellationToken cancellationToken)
        {
            var classifier = await db.BookClasificators.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
            if (classifier == null)
            {
                return CommonErrors.NotFound("Classifier", request.Id);
            }

            if (await db.BookClasificators.AnyAsync(c => c.ParentId == request.Id, cancellationToken))
            {
                return CommonErrors.InUse("Classifier", request.Id, "it has sub-classifiers");
            }

            // Books keep existing; their classifier is cleared by the foreign key (ON DELETE SET NULL).
            db.BookClasificators.Remove(classifier);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }
}
