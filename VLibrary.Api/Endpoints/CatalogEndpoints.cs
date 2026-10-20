using MediatR;
using VLibrary.Api.Infrastructure;
using VLibrary.Application.Features.Authors;
using VLibrary.Application.Features.Classifiers;
using VLibrary.Application.Features.Tags;

namespace VLibrary.Api.Endpoints
{
    public sealed record ClassifierRequest(string Title, long? ParentId);

    /// <summary>Authors, tags and the classification tree.</summary>
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

            var classifiers = api.MapGroup("/classifiers").WithTags("Classifiers");

            classifiers.MapGet("/", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetClassifierTreeQuery(), ct)).ToHttp());

            classifiers.MapPost("/", async (ClassifierRequest request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateClassifierCommand(request.Title, request.ParentId), ct);
                return result.IsSuccess
                    ? Results.Created($"/api/v1/classifiers/{result.Value}", new { id = result.Value })
                    : result.Error.ToProblem();
            });

            classifiers.MapPut("/{id:long}", async (long id, ClassifierRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateClassifierCommand(id, request.Title, request.ParentId), ct)).ToHttp());

            classifiers.MapDelete("/{id:long}", async (long id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DeleteClassifierCommand(id), ct)).ToHttp());

            return api;
        }
    }
}
