using MediatR;
using VLibrary.Api.Infrastructure;
using VLibrary.Application.Features.Categories;
using VLibrary.Application.Features.Library;

namespace VLibrary.Api.Endpoints
{
    /// <summary>Categories (the library tree) and the VStorage availability check.</summary>
    public static class LibraryEndpoints
    {
        public static RouteGroupBuilder MapLibraryEndpoints(this RouteGroupBuilder api)
        {
            api.MapGet("/categories", async (ISender sender, CancellationToken ct) =>
                    (await sender.Send(new GetCategoriesQuery(), ct)).ToHttp())
                .WithTags("Categories")
                .WithSummary("All categories (flat, ordered by path) with file counts; the tree comes from parentId.");

            api.MapPost("/library/check", async (ISender sender, CancellationToken ct) =>
                    (await sender.Send(new CheckLibraryCommand(), ct)).ToHttp())
                .WithTags("Library")
                .WithSummary("Check that every file is still in VStorage: Ok / Missing. 503 when VStorage is unavailable.");

            return api;
        }
    }
}
