using MediatR;
using VLibrary.Api.Infrastructure;
using VLibrary.Application.Features.Backup;
using VLibrary.Application.Features.Categories;
using VLibrary.Application.Features.Library;

namespace VLibrary.Api.Endpoints
{
    public sealed record CategoryRequest(string Name, long? ParentId);

    /// <summary>Categories (the library tree), the VStorage availability check and the backup file list.</summary>
    public static class LibraryEndpoints
    {
        public static RouteGroupBuilder MapLibraryEndpoints(this RouteGroupBuilder api)
        {
            var categories = api.MapGroup("/categories").WithTags("Categories");

            categories.MapGet("/", async (ISender sender, CancellationToken ct) =>
                    (await sender.Send(new GetCategoriesQuery(), ct)).ToHttp())
                .WithSummary("Live categories (flat, ordered by path) with file counts and the old names that lead to them; " +
                             "the tree comes from parentId.");

            categories.MapPost("/", async (CategoryRequest request, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(new CreateCategoryCommand(request.Name, request.ParentId), ct);
                    return result.IsSuccess
                        ? Results.Created($"/api/v1/categories/{result.Value}", new { id = result.Value })
                        : result.Error.ToProblem();
                })
                .WithSummary("A new empty category (parentId null = top level).");

            categories.MapPut("/{id:long}", async (long id, CategoryRequest request, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new UpdateCategoryCommand(id, request.Name, request.ParentId), ct)).ToHttp())
                .WithSummary("Rename and/or move with all subcategories and files; a same-named category at the target is merged. " +
                             "The old name stays as a redirect for imports; VStorage keys follow. 409 = file name clash or import running, " +
                             "503 = VStorage unavailable.");

            categories.MapDelete("/{id:long}", async (long id, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new DeleteCategoryCommand(id), ct)).ToHttp())
                .WithSummary("Forget an old name (redirect), or delete a category without subcategories and files.");

            api.MapPost("/library/check", async (ISender sender, CancellationToken ct) =>
                    (await sender.Send(new CheckLibraryCommand(), ct)).ToHttp())
                .WithTags("Library")
                .WithSummary("Check that every file is still in VStorage: Ok / Missing. 503 when VStorage is unavailable.");

            api.MapGet("/backup/files", async (ISender sender, CancellationToken ct) =>
                    (await sender.Send(new GetBackupManifestQuery(), ct)).ToHttp())
                .WithTags("Backup")
                .WithSummary("All library files with their category path, for the backup into folders like the categories.");

            return api;
        }
    }
}
