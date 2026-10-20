using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VLibrary.Application.Abstractions;
using VLibrary.Application.Common;
using VLibrary.Domain.Common;
using VLibrary.Domain.Entities;
using VLibrary.Domain.Enums;

namespace VLibrary.Application.Features.Covers
{
    /// <summary>The cover a book or file shows: GET /files/{FileId}/cover?v={Version}.</summary>
    public sealed record CoverDto(long FileId, long Version);

    public sealed record CoverContentDto(byte[] Data, string ContentType, long Version);

    /// <param name="WithCover">Files that have a cover.</param>
    /// <param name="Pending">Files of a supported format not tried yet (for "Generate missing covers").</param>
    /// <param name="WithoutCover">Files that were tried but have no cover (damaged, no image, tool missing).</param>
    public sealed record CoverStatusDto(int WithCover, int Pending, int WithoutCover);

    public sealed record GenerateCoversResultDto(int Processed, int Generated, int Remaining);

    public sealed record GetCoverQuery(long FileId) : IRequest<Result<CoverContentDto>>;

    public sealed record GetCoverStatusQuery : IRequest<Result<CoverStatusDto>>;

    /// <summary>
    /// Makes covers for up to <paramref name="Take"/> files that were never tried (files imported before covers existed):
    /// each file is downloaded from VStorage with the user's token. The browser repeats the call until Remaining is 0.
    /// </summary>
    public sealed record GenerateCoversCommand(int Take = 5) : IRequest<Result<GenerateCoversResultDto>>;

    /// <summary>Files tried without result are tried again by the next "Generate" (e.g. after installing DjVuLibre).</summary>
    public sealed record ResetFailedCoversCommand : IRequest<Result<int>>;

    internal sealed class GenerateCoversValidator : AbstractValidator<GenerateCoversCommand>
    {
        public GenerateCoversValidator() => RuleFor(c => c.Take).InclusiveBetween(1, 20);
    }

    /// <summary>Renders the cover of a local copy of a file and attaches it (the caller saves).</summary>
    internal sealed class CoverAttacher(ICoverRenderer renderer, IClock clock)
    {
        public async Task<bool> AttachAsync(FileMetaEntity file, string localPath, CancellationToken cancellationToken)
        {
            var image = renderer.SupportedFormats.Contains(file.Format)
                ? await renderer.RenderAsync(localPath, file.Format, cancellationToken)
                : null;
            if (image == null)
            {
                file.MarkCoverChecked(clock.UtcNow);
                return false;
            }

            file.SetCover(image.Width, image.Height, image.Jpeg, clock.UtcNow);
            return true;
        }
    }

    internal sealed class CoverHandlers(
        IAppDbContext db,
        ICoverRenderer renderer,
        CoverAttacher attacher,
        IBookStorage storage,
        IUploadStaging staging,
        IClock clock,
        ILogger<CoverHandlers> logger)
        : IRequestHandler<GetCoverQuery, Result<CoverContentDto>>,
          IRequestHandler<GetCoverStatusQuery, Result<CoverStatusDto>>,
          IRequestHandler<GenerateCoversCommand, Result<GenerateCoversResultDto>>,
          IRequestHandler<ResetFailedCoversCommand, Result<int>>
    {
        public async Task<Result<CoverContentDto>> Handle(GetCoverQuery request, CancellationToken cancellationToken)
        {
            var cover = await db.FileCovers.AsNoTracking()
                .Where(c => c.FileMetaId == request.FileId)
                .Select(c => new { c.Data, c.ContentType, c.UpdatedAt })
                .FirstOrDefaultAsync(cancellationToken);

            return cover == null
                ? CommonErrors.NotFound("Cover of file", request.FileId)
                : new CoverContentDto(cover.Data, cover.ContentType, FileCoverEntity.VersionOf(cover.UpdatedAt));
        }

        public async Task<Result<CoverStatusDto>> Handle(GetCoverStatusQuery request, CancellationToken cancellationToken)
        {
            var formats = renderer.SupportedFormats.ToList();
            var withCover = await db.FileMetas.CountAsync(f => f.Cover != null, cancellationToken);
            var pending = await Pending(formats).CountAsync(cancellationToken);
            var withoutCover = await db.FileMetas.CountAsync(f => f.Cover == null && f.CoverCheckedAt != null, cancellationToken);
            return new CoverStatusDto(withCover, pending, withoutCover);
        }

        public async Task<Result<GenerateCoversResultDto>> Handle(GenerateCoversCommand request, CancellationToken cancellationToken)
        {
            var formats = renderer.SupportedFormats.ToList();
            var files = await Pending(formats).OrderBy(f => f.Id).Take(request.Take).ToListAsync(cancellationToken);

            int processed = 0, generated = 0;
            foreach (var file in files)
            {
                var content = await storage.OpenReadAsync(file.StorageObjectId, cancellationToken);
                if (content.IsFailure)
                {
                    if (content.Error.Type == ErrorType.Unavailable)
                    {
                        // Storage outage: this file stays pending; the browser pauses and retries later.
                        return content.Error;
                    }

                    // Object gone: the library check marks the file Missing; no cover can be made.
                    file.MarkCoverChecked(clock.UtcNow);
                }
                else
                {
                    await using var stream = content.Value;
                    var staged = await staging.StageAsync(stream, file.FileName, cancellationToken);
                    if (staged.IsFailure)
                    {
                        file.MarkCoverChecked(clock.UtcNow);
                    }
                    else
                    {
                        await using var local = staged.Value;
                        if (await attacher.AttachAsync(file, local.Path, cancellationToken))
                        {
                            generated++;
                        }
                    }
                }

                processed++;
                await db.SaveChangesAsync(cancellationToken);
            }

            var remaining = await Pending(formats).CountAsync(cancellationToken);
            logger.LogInformation("Covers: {Processed} file(s) processed, {Generated} cover(s) made, {Remaining} remaining",
                processed, generated, remaining);
            return new GenerateCoversResultDto(processed, generated, remaining);
        }

        public async Task<Result<int>> Handle(ResetFailedCoversCommand request, CancellationToken cancellationToken) =>
            await db.FileMetas
                .Where(f => f.Cover == null && f.CoverCheckedAt != null)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.CoverCheckedAt, (DateTime?)null), cancellationToken);

        /// <summary>Live, available files of a supported format that were never tried.</summary>
        private IQueryable<FileMetaEntity> Pending(List<FileTypesEnum> formats) =>
            db.FileMetas.Where(f => f.CoverCheckedAt == null && f.Status == FileStatus.Ok && formats.Contains(f.Format));
    }
}
