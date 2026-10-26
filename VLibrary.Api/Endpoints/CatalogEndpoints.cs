using MediatR;
using VLibrary.Api.Infrastructure;
using VLibrary.Application.Features.Authors;
using VLibrary.Application.Features.Tags;

namespace VLibrary.Api.Endpoints
{
    /// <summary>Authors and tags.</summary>
    public static class CatalogEndpoints
    {
        public static RouteGroupBuilder MapCatalogEndpoints(this RouteGroupBuilder api)
        {
            var authors = api.MapGroup("/authors").WithTags("Authors");

            authors.MapGet("/", async (string? q, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetAuthorsQuery(q, page ?? 1, pageSize ?? 50), ct)).ToHttp());

            authors.MapGet("/{id:long}", async (long id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetAuthorQuery(id), ct)).ToHttp());

            authors.MapPost("/", async (AuthorFields fields, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateAuthorCommand(fields), ct);
                return result.IsSuccess
                    ? Results.Created($"/api/v1/authors/{result.Value}", new { id = result.Value })
                    : result.Error.ToProblem();
            });

            authors.MapPut("/{id:long}", async (long id, AuthorFields fields, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateAuthorCommand(id, fields), ct)).ToHttp());

            authors.MapDelete("/{id:long}", async (long id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DeleteAuthorCommand(id), ct)).ToHttp());

            api.MapGroup("/tags").WithTags("Tags")
                .MapGet("/", async (string? q, int? take, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new GetTagsQuery(q, take ?? 30), ct)).ToHttp())
                .WithSummary("Tag autocomplete.");

            return api;
        }
    }
}
