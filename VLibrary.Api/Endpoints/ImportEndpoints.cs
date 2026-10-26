using MediatR;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using VLibrary.Api.Infrastructure;
using VLibrary.Application.Features.Imports;
using VLibrary.Domain.Common;
using VLibrary.Domain.Enums;
using VLibrary.Domain.Rules;
using VLibrary.Infrastructure.Files;

namespace VLibrary.Api.Endpoints
{
    public sealed record CreateImportRequest(string SourceName, int TotalFiles, long? ParentCategoryId);

    public sealed record PrecheckImportRequest(IReadOnlyList<PrecheckFileDto> Files);

    public sealed record ResumeImportRequest(int TotalFiles);

    /// <summary>What the browser may upload: it filters the picked folder with these before starting.</summary>
    public sealed record ImportOptionsDto(IReadOnlyList<string> SupportedExtensions, IReadOnlyList<string> ExcludedFolders, long MaxFileBytes);

    /// <summary>
    /// UC-1 as a browser-driven upload: create -> (precheck) -> one POST per file -> complete.
    /// Files are streamed through this API to VStorage; VLibrary keeps only the meta info.
    /// </summary>
    public static class ImportEndpoints
    {
        private const int MaxFieldChars = 2048;
        private const long MultipartOverheadBytes = 64 * 1024;

        private static readonly Error ExpectedMultipart =
            Error.Validation("Import.ExpectedMultipart", "Content-Type must be multipart/form-data with a boundary.");
        private static readonly Error FieldOrder =
            Error.Validation("Import.FieldOrder", "The field 'relativePath' (and optional 'lastModified') must come before the file.");
        private static readonly Error FieldTooLong =
            Error.Validation("Import.FieldTooLong", $"Form fields are limited to {MaxFieldChars} characters.");
        private static readonly Error MissingFile = Error.Validation("Import.MissingFile", "The form has no file.");

        public static RouteGroupBuilder MapImportEndpoints(this RouteGroupBuilder api)
        {
            var group = api.MapGroup("/imports").WithTags("Imports");

            group.MapGet("/options", (IOptions<ImportOptions> options) => Results.Ok(new ImportOptionsDto(
                    FileFormats.SupportedExtensions.Order().ToList(), options.Value.ExcludedFolders, options.Value.MaxFileBytes)))
                .WithSummary("Supported extensions, excluded folder names and the size limit for the upload picker.");

            group.MapPost("/", async (CreateImportRequest request, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(new CreateImportCommand(request.SourceName, request.TotalFiles, request.ParentCategoryId), ct);
                    return result.IsSuccess
                        ? Results.Created($"/api/v1/imports/{result.Value}", new { id = result.Value })
                        : result.Error.ToProblem();
                })
                .WithSummary("UC-1: start an import of files picked in the browser (then precheck, upload each file, complete).");

            group.MapPost("/{id:long}/precheck", async (long id, PrecheckImportRequest request, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new PrecheckImportCommand(id, request.Files), ct)).ToHttp())
                .WithSummary("Up to 1000 files {relativePath, size, lastModified}: returns those that need no upload (unchanged).");

            group.MapPost("/{id:long}/files", UploadFile)
                .WithSummary("Upload one file (multipart/form-data): relativePath, lastModified (Unix ms), then the file. " +
                             "503 = VStorage unavailable, nothing recorded: retry the same file later.")
                .Produces<ImportFileResultDto>()
                .DisableAntiforgery();

            group.MapPost("/{id:long}/complete", async (long id, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new CompleteImportCommand(id), ct)).ToHttp())
                .WithSummary("All files were sent.");

            group.MapPost("/{id:long}/cancel", async (long id, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new CancelImportCommand(id), ct)).ToHttp())
                .WithSummary("The user stopped the upload.");

            group.MapPost("/{id:long}/resume", async (long id, ResumeImportRequest request, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new ResumeImportCommand(id, request.TotalFiles), ct)).ToHttp())
                .WithSummary("Continue a stopped import with the same files picked again (then precheck, upload, complete). Files it " +
                             "already handled are skipped and not counted again; failed files are tried again. 409 = completed or failed.");

            group.MapGet("/", async (int? take, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new GetImportsQuery(take ?? 20), ct)).ToHttp())
                .WithSummary("Recent imports, newest first.");

            group.MapGet("/{id:long}", async (long id, ImportOutcome? outcome, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new GetImportQuery(id, outcome, page ?? 1, pageSize ?? 100), ct)).ToHttp())
                .WithSummary("Import progress and per-file results.");

            return api;
        }

        /// <summary>Streams the file section into the import (no IFormFile buffering).</summary>
        private static async Task<IResult> UploadFile(
            long id, HttpContext http, ISender sender, IOptions<ImportOptions> options, CancellationToken cancellationToken)
        {
            var bodySize = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (bodySize is { IsReadOnly: false })
            {
                bodySize.MaxRequestBodySize = options.Value.MaxFileBytes + MultipartOverheadBytes;
            }

            if (!MediaTypeHeaderValue.TryParse(http.Request.ContentType, out var mediaType)
                || !string.Equals(mediaType.MediaType.Value, "multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                return ExpectedMultipart.ToProblem();
            }

            var boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value;
            if (string.IsNullOrEmpty(boundary))
            {
                return ExpectedMultipart.ToProblem();
            }

            var reader = new MultipartReader(boundary, http.Request.Body);
            string? relativePath = null;
            DateTime? lastModified = null;
            while (await reader.ReadNextSectionAsync(cancellationToken) is { } section)
            {
                if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition))
                {
                    continue;
                }

                if (disposition.IsFileDisposition())
                {
                    if (relativePath == null)
                    {
                        return FieldOrder.ToProblem();
                    }

                    var result = await sender.Send(new ImportFileCommand(id, relativePath, lastModified, section.Body), cancellationToken);
                    return result.ToHttp();
                }

                var value = await ReadFieldAsync(section, cancellationToken);
                if (value == null)
                {
                    return FieldTooLong.ToProblem();
                }

                switch (HeaderUtilities.RemoveQuotes(disposition.Name).Value)
                {
                    case "relativePath":
                        relativePath = value;
                        break;
                    case "lastModified" when long.TryParse(value, out var milliseconds):
                        lastModified = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime;
                        break;
                }
            }

            return MissingFile.ToProblem();
        }

        /// <summary>A text field, or null when it is longer than <see cref="MaxFieldChars"/>.</summary>
        private static async Task<string?> ReadFieldAsync(MultipartSection section, CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(section.Body);
            var buffer = new char[MaxFieldChars + 1];
            var read = await reader.ReadBlockAsync(buffer, cancellationToken);
            return read > MaxFieldChars ? null : new string(buffer, 0, read);
        }
    }
}
