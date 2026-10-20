using MediatR;
using Microsoft.EntityFrameworkCore;
using VLibrary.Application.Abstractions;
using VLibrary.Domain.Common;
using VLibrary.Domain.Entities;
using VLibrary.Domain.Enums;
using VLibrary.Domain.ValueObjects;

namespace VLibrary.Application.Features.Duplicates
{
    public sealed record ExtractedMetadataDto(
        string? Title, IReadOnlyList<string> Authors, string? Isbn, int? Year, string? Publisher, string Source);

    public sealed record DuplicateCandidateDto(
        long BookId,
        string Title,
        IReadOnlyList<string> Authors,
        string? Isbn,
        int? YearPublished,
        int FilesCount,
        double Score,
        DuplicateReason Reason);

    public sealed record DuplicateReviewDto(
        long FileMetaId,
        string LibraryPath,
        string FileName,
        FileTypesEnum Format,
        long SizeBytes,
        DateTime ImportedAt,
        ExtractedMetadataDto? Metadata,
        IReadOnlyList<DuplicateCandidateDto> Candidates,
        long? CoverVersion);

    /// <summary>UC-2: the duplicate review queue, oldest first.</summary>
    public sealed record GetDuplicatesQuery : IRequest<Result<IReadOnlyList<DuplicateReviewDto>>>;

    internal sealed class GetDuplicatesHandler(IAppDbContext db)
        : IRequestHandler<GetDuplicatesQuery, Result<IReadOnlyList<DuplicateReviewDto>>>
    {
        public async Task<Result<IReadOnlyList<DuplicateReviewDto>>> Handle(GetDuplicatesQuery request, CancellationToken cancellationToken)
        {
            var files = await db.FileMetas
                .AsNoTracking()
                .Where(f => f.BookId == null && f.DuplicateCandidates.Any(c => c.ResolvedAt == null))
                .OrderBy(f => f.Id)
                .Select(f => new
                {
                    f.Id,
                    f.StorageKey,
                    f.FileName,
                    f.Format,
                    f.SizeBytes,
                    f.CreatedAt,
                    f.ExtractedMetadata,
                    CoverUpdatedAt = f.Cover != null ? f.Cover.UpdatedAt : (DateTime?)null,
                    Candidates = f.DuplicateCandidates
                        .Where(c => c.ResolvedAt == null)
                        .OrderByDescending(c => c.Score).ThenBy(c => c.Id)
                        .Select(c => new
                        {
                            c.CandidateBookId,
                            c.CandidateBook.Title,
                            c.CandidateBook.ISBN,
                            c.CandidateBook.YearPublished,
                            FilesCount = c.CandidateBook.FilesMetas.Count,
                            c.Score,
                            c.Reason,
                            Authors = c.CandidateBook.BookAuthors
                                .OrderBy(ba => ba.Order)
                                .Select(ba => new { ba.Author.LastName, ba.Author.FirstName, ba.Author.MiddleName, ba.Author.NameInitials })
                                .ToList()
                        })
                        .ToList()
                })
                .ToListAsync(cancellationToken);

            return files
                .Select(f => new DuplicateReviewDto(
                    f.Id,
                    f.StorageKey,
                    f.FileName,
                    f.Format,
                    f.SizeBytes,
                    f.CreatedAt,
                    ToDto(f.ExtractedMetadata),
                    f.Candidates
                        .Select(c => new DuplicateCandidateDto(
                            c.CandidateBookId,
                            c.Title,
                            c.Authors.Select(a => PersonName.Format(a.LastName, a.FirstName, a.MiddleName, a.NameInitials)).ToList(),
                            c.ISBN,
                            c.YearPublished,
                            c.FilesCount,
                            c.Score,
                            c.Reason))
                        .ToList(),
                    f.CoverUpdatedAt is { } coverUpdatedAt ? FileCoverEntity.VersionOf(coverUpdatedAt) : null))
                .ToList();
        }

        private static ExtractedMetadataDto? ToDto(ExtractedMetadata? metadata) =>
            metadata == null
                ? null
                : new ExtractedMetadataDto(metadata.Title, metadata.Authors, metadata.Isbn, metadata.Year, metadata.Publisher, metadata.Source);
    }
}
