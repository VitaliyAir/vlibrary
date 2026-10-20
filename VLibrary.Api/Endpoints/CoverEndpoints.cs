using MediatR;
using Microsoft.Net.Http.Headers;
using VLibrary.Api.Infrastructure;
using VLibrary.Application.Features.Covers;

namespace VLibrary.Api.Endpoints
{
    /// <summary>Cover thumbnails (B1): the image of a file, status and the "Generate missing covers" batches.</summary>
    public static class CoverEndpoints
    {
        public static RouteGroupBuilder MapCoverEndpoints(this RouteGroupBuilder api)
        {
            api.MapGet("/files/{id:long}/cover", async (long id, HttpContext http, ISender sender, CancellationToken ct) =>
                {
                    var cover = await sender.Send(new GetCoverQuery(id), ct);
                    if (cover.IsFailure)
                    {
                        return cover.Error.ToProblem();
                    }

                    // The URL carries ?v=<version>; a new cover gets a new URL, so the browser may keep this one forever.
                    http.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
                    return Results.File(
                        cover.Value.Data,
                        cover.Value.ContentType,
                        entityTag: new EntityTagHeaderValue($"\"{cover.Value.Version}\""));
                })
                .WithTags("Covers")
                .WithSummary("Cover thumbnail of a file (JPEG); ?v=<cover version> for caching. 404 when the file has no cover.");

            var covers = api.MapGroup("/covers").WithTags("Covers");

            covers.MapGet("/status", async (ISender sender, CancellationToken ct) =>
                    (await sender.Send(new GetCoverStatusQuery(), ct)).ToHttp())
                .WithSummary("Files with a cover, files not tried yet, files tried without result.");

            covers.MapPost("/generate", async (int? take, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new GenerateCoversCommand(take ?? 5), ct)).ToHttp())
                .WithSummary("Make covers for up to `take` (1..20) files not tried yet (downloaded from VStorage). " +
                             "Repeat until remaining = 0; 503 = VStorage unavailable.");

            covers.MapPost("/reset-failed", async (ISender sender, CancellationToken ct) =>
                    (await sender.Send(new ResetFailedCoversCommand(), ct)).ToHttp())
                .WithSummary("Files tried without a cover are tried again by the next generate (e.g. after installing DjVuLibre).");

            return api;
        }
    }
}
