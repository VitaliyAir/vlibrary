using MediatR;
using VLibrary.Api.Infrastructure;
using VLibrary.Application.Features.Books;
using VLibrary.Domain.Enums;

namespace VLibrary.Api.Endpoints
{
    public sealed record SetRatingRequest(int? Rating);

    public sealed record SetStatusRequest(ReadingStatus Status);

    public sealed record SetTagsRequest(IReadOnlyList<string> Tags);

    public static class BookEndpoints
    {
        public static RouteGroupBuilder MapBookEndpoints(this RouteGroupBuilder api)
        {
            var group = api.MapGroup("/books").WithTags("Books");

            group.MapGet("/", async (
                    string? q, long? shelfId, bool? onShelf, ReadingStatus? status, string? tag, FileTypesEnum? format,
                    int? minRating, long? authorId, long? categoryId, BookSort? sort, int? page, int? pageSize,
                    ISender sender, CancellationToken ct) =>
                    (await sender.Send(new SearchBooksQuery(
                        q, shelfId, onShelf, status, tag, format, minRating, authorId, categoryId,
                        sort ?? BookSort.Title, page ?? 1, pageSize ?? 50), ct)).ToHttp())
                .WithSummary("UC-4: search and filter the catalog.");

            group.MapGet("/{id:long}", async (long id, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new GetBookQuery(id), ct)).ToHttp())
                .WithSummary("UC-3: the book card.");

            group.MapPost("/", async (BookFields fields, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(new CreateBookCommand(fields), ct);
                    return result.IsSuccess
                        ? Results.Created($"/api/v1/books/{result.Value}", new { id = result.Value })
                        : result.Error.ToProblem();
                })
                .WithSummary("Add a book by hand (without a file).");

            group.MapPut("/{id:long}", async (long id, BookFields fields, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new UpdateBookCommand(id, fields), ct)).ToHttp())
                .WithSummary("Edit the book card (authors and tags given as names).");

            group.MapDelete("/{id:long}", async (long id, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new DeleteBookCommand(id), ct)).ToHttp())
                .WithSummary("Remove the book from the catalog: its notes and shelf links are deleted, files stay in VStorage.");

            group.MapPut("/{id:long}/rating", async (long id, SetRatingRequest request, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new SetBookRatingCommand(id, request.Rating), ct)).ToHttp())
                .WithSummary("Rating 1-10, or null to clear.");

            group.MapPut("/{id:long}/status", async (long id, SetStatusRequest request, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new SetBookStatusCommand(id, request.Status), ct)).ToHttp())
                .WithSummary("Reading status.");

            group.MapPut("/{id:long}/tags", async (long id, SetTagsRequest request, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new SetBookTagsCommand(id, request.Tags), ct)).ToHttp())
                .WithSummary("Replace the book's tags (missing tags are created).");

            group.MapPost("/{id:long}/files/{fileId:long}/primary", async (long id, long fileId, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new SetPrimaryFileCommand(id, fileId), ct)).ToHttp())
                .WithSummary("Open the book with this file from now on (another copy or format).");

            group.MapPost("/{id:long}/files/{fileId:long}/detach", async (long id, long fileId, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new DetachFileCommand(id, fileId), ct)).ToHttp())
                .WithSummary("Keep the book without this file; the file record stays as meta info only.");

            return api;
        }
    }
}
