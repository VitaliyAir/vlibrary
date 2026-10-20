using MediatR;
using VLibrary.Api.Infrastructure;
using VLibrary.Application.Features.Shelves;

namespace VLibrary.Api.Endpoints
{
    public sealed record ShelfRequest(string Name, string? Description);

    public sealed record OrderRequest(IReadOnlyList<long> Ids);

    public static class ShelfEndpoints
    {
        public static RouteGroupBuilder MapShelfEndpoints(this RouteGroupBuilder api)
        {
            var group = api.MapGroup("/shelves").WithTags("Shelves");

            group.MapGet("/", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetShelvesQuery(), ct)).ToHttp());

            group.MapGet("/{id:long}", async (long id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetShelfQuery(id), ct)).ToHttp());

            group.MapPost("/", async (ShelfRequest request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateShelfCommand(request.Name, request.Description), ct);
                return result.IsSuccess
                    ? Results.Created($"/api/v1/shelves/{result.Value}", new { id = result.Value })
                    : result.Error.ToProblem();
            });

            group.MapPut("/{id:long}", async (long id, ShelfRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateShelfCommand(id, request.Name, request.Description), ct)).ToHttp());

            group.MapDelete("/{id:long}", async (long id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DeleteShelfCommand(id), ct)).ToHttp());

            group.MapPut("/order", async (OrderRequest request, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new ReorderShelvesCommand(request.Ids), ct)).ToHttp())
                .WithSummary("Shelf ids in the new display order.");

            group.MapPost("/{id:long}/books/{bookId:long}", async (long id, long bookId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AddBookToShelfCommand(id, bookId), ct)).ToHttp());

            group.MapDelete("/{id:long}/books/{bookId:long}", async (long id, long bookId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new RemoveBookFromShelfCommand(id, bookId), ct)).ToHttp());

            group.MapPut("/{id:long}/books/order", async (long id, OrderRequest request, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new ReorderShelfBooksCommand(id, request.Ids), ct)).ToHttp())
                .WithSummary("Book ids of the shelf in the new order.");

            return api;
        }
    }
}
