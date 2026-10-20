using MediatR;
using VLibrary.Api.Infrastructure;
using VLibrary.Application.Features.Files;
using VLibrary.Application.Features.Notes;
using VLibrary.Application.Features.ReadingRoom;
using VLibrary.Application.Features.Reminders;
using VLibrary.Application.Features.Settings;
using VLibrary.Domain.Enums;

namespace VLibrary.Api.Endpoints
{
    public sealed record ReviewRequest(ReviewAnswer Answer);

    /// <summary>Files, reading room, notes, reminders and settings (UC-5, UC-6, UC-8).</summary>
    public static class ReadingEndpoints
    {
        public static RouteGroupBuilder MapReadingEndpoints(this RouteGroupBuilder api)
        {
            var files = api.MapGroup("/files").WithTags("Files");

            files.MapGet("/", async (bool? linked, FileStatus? status, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetFilesQuery(linked, status, page ?? 1, pageSize ?? 100), ct)).ToHttp());

            files.MapGet("/{id:long}", async (long id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetFileQuery(id), ct)).ToHttp());

            files.MapPost("/{id:long}/opened", async (long id, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new MarkFileOpenedCommand(id), ct)).ToHttp())
                .WithSummary("UC-5: open a file: returns a short-lived VStorage link and records the open.");

            api.MapGet("/reading-room", async (int? booksPerList, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new GetReadingRoomQuery(booksPerList ?? 12), ct)).ToHttp())
                .WithTags("Reading room")
                .WithSummary("UC-5: recently opened, in progress and shelf lists.");

            var notes = api.MapGroup("/notes").WithTags("Notes");

            api.MapGet("/books/{bookId:long}/notes", async (long bookId, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new GetBookNotesQuery(bookId), ct)).ToHttp())
                .WithTags("Notes");

            api.MapPost("/books/{bookId:long}/notes", async (long bookId, NoteFields fields, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(new CreateNoteCommand(bookId, fields), ct);
                    return result.IsSuccess
                        ? Results.Created($"/api/v1/notes/{result.Value}", new { id = result.Value })
                        : result.Error.ToProblem();
                })
                .WithTags("Notes")
                .WithSummary("UC-6: add a note or quote with a page number.");

            notes.MapGet("/search", async (string? q, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SearchNotesQuery(q, page ?? 1, pageSize ?? 50), ct)).ToHttp());

            notes.MapPut("/{id:long}", async (long id, NoteFields fields, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateNoteCommand(id, fields), ct)).ToHttp());

            notes.MapDelete("/{id:long}", async (long id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DeleteNoteCommand(id), ct)).ToHttp());

            var reminders = api.MapGroup("/reminders").WithTags("Reminders");

            reminders.MapGet("/next", async (ISender sender, CancellationToken ct) =>
                    (await sender.Send(new GetRemindersQuery(), ct)).ToHttp())
                .WithSummary("UC-8: notes to show at start (empty when switched off).");

            reminders.MapPost("/{noteId:long}/review", async (long noteId, ReviewRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ReviewNoteCommand(noteId, request.Answer), ct)).ToHttp());

            var settings = api.MapGroup("/settings").WithTags("Settings");

            settings.MapGet("/", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetSettingsQuery(), ct)).ToHttp());

            settings.MapPut("/", async (SettingsDto request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateSettingsCommand(request), ct)).ToHttp());

            return api;
        }
    }
}
