using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VLibrary.Application.Abstractions;
using VLibrary.Application.Common;
using VLibrary.Application.Features.Imports;
using VLibrary.Domain.Common;
using VLibrary.Domain.Entities;
using VLibrary.Domain.Enums;

namespace VLibrary.Application.Features.Files
{
    /// <param name="Name">The new name without the extension, e.g. "Astronomy".</param>
    /// <param name="UseAsTitle">Also make it the title of the file's book.</param>
    public sealed record RenameFileCommand(long Id, string Name, bool UseAsTitle) : IRequest<Result<FileRenamedDto>>;

    /// <param name="LibraryPath">The new VStorage key, e.g. "Library/Sky/Astronomy.pdf".</param>
    public sealed record FileRenamedDto(long FileId, string FileName, string LibraryPath, bool MissingInStorage);

    internal sealed class RenameFileValidator : AbstractValidator<RenameFileCommand>
    {
        public RenameFileValidator() => RuleFor(c => c.Name).NotEmpty().MaximumLength(FileMetaEntity.MaxBaseNameLength);
    }

    internal static class FileErrors
    {
        public static readonly Error KeyTooLong = Error.Validation(
            "File.Name", $"The VStorage key would be longer than {ImportFileProcessor.MaxKeyBytes} bytes. Use a shorter name.");

        public static Error NameTaken(string path) => Error.Conflict(
            "File.NameTaken", $"The category already has a file named '{path}' (names are compared ignoring case).");
    }

    /// <summary>
    /// Renames a book file (the extension stays) in VLibrary and VStorage; the original disk name is remembered so an
    /// import of the disk file finds it.
    /// </summary>
    internal sealed class RenameFileHandler(IAppDbContext db, IBookStorage storage) : IRequestHandler<RenameFileCommand, Result<FileRenamedDto>>
    {
        public async Task<Result<FileRenamedDto>> Handle(RenameFileCommand request, CancellationToken cancellationToken)
        {
            var file = await db.FileMetas
                .Include(f => f.Category)
                .Include(f => f.Book)
                .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);
            if (file == null)
            {
                return CommonErrors.NotFound("File", request.Id);
            }

            var oldName = file.FileName;
            var renamed = file.Rename(request.Name);
            if (renamed.IsFailure)
            {
                return renamed.Error;
            }

            if (!string.Equals(file.FileName, oldName, StringComparison.Ordinal))
            {
                // One name per category ignoring case (a Windows backup could not hold both); removed files count too.
                var lower = file.FileName.ToLower();
                if (await db.FileMetas.IgnoreQueryFilters().AnyAsync(
                        f => f.CategoryId == file.CategoryId && f.Id != file.Id && f.FileName.ToLower() == lower, cancellationToken))
                {
                    return FileErrors.NameTaken(file.StorageKey);
                }

                if (Encoding.UTF8.GetByteCount(file.StorageKey) > ImportFileProcessor.MaxKeyBytes)
                {
                    return FileErrors.KeyTooLong;
                }

                // An original name leads to one file only: the file that takes the name now.
                var holders = await db.FileMetas.IgnoreQueryFilters()
                    .Where(f => f.CategoryId == file.CategoryId && f.Id != file.Id && f.OriginalFileName == file.FileName)
                    .ToListAsync(cancellationToken);
                foreach (var holder in holders)
                {
                    holder.ForgetOriginalName();
                }

                var moved = await storage.RenameAsync([new StorageRename(file.StorageObjectId, file.StorageKey)], cancellationToken);
                if (moved.IsFailure)
                {
                    return moved.Error;
                }

                if (moved.Value.Missing.Count > 0)
                {
                    file.MarkMissing();
                }
            }

            if (request.UseAsTitle && file.Book != null)
            {
                file.Book.Title = request.Name.Trim();
            }

            await db.SaveChangesAsync(cancellationToken);
            return new FileRenamedDto(file.Id, file.FileName, file.StorageKey, file.Status == FileStatus.Missing);
        }
    }
}
