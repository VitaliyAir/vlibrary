using MediatR;
using VLibrary.Api.Infrastructure;
using VLibrary.Application.Features.Duplicates;

namespace VLibrary.Api.Endpoints
{
    public sealed record LinkDuplicateRequest(long BookId);

    public static class DuplicateEndpoints
    {
        public static RouteGroupBuilder MapDuplicateEndpoints(this RouteGroupBuilder api)
        {
            var group = api.MapGroup("/duplicates").WithTags("Duplicates");

            group.MapGet("/", async (ISender sender, CancellationToken ct) =>
                    (await sender.Send(new GetDuplicatesQuery(), ct)).ToHttp())
                .WithSummary("UC-2: files waiting for duplicate review, with candidate books.");

            group.MapPost("/{fileId:long}/link", async (long fileId, LinkDuplicateRequest request, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new LinkDuplicateCommand(fileId, request.BookId), ct)).ToHttp())
                .WithSummary("Link the file to an existing book.");

            group.MapPost("/{fileId:long}/create-book", async (long fileId, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(new CreateBookFromDuplicateCommand(fileId), ct);
                    return result.IsSuccess
                        ? Results.Created($"/api/v1/books/{result.Value}", new { id = result.Value })
                        : result.Error.ToProblem();
                })
                .WithSummary("Create a new book from the file's metadata.");

            group.MapDelete("/{fileId:long}", async (long fileId, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new RemoveDuplicateCommand(fileId), ct)).ToHttp())
                .WithSummary("Remove the file from the library (the file stays on disk).");

            return api;
        }
    }
}
