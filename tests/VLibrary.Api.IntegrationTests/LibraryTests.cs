using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VLibrary.Infrastructure.Persistence;

namespace VLibrary.Api.IntegrationTests
{
    /// <summary>Same content elsewhere, deleted-book duplicates, name conflicts, opening and the VStorage check.</summary>
    [Collection(ApiCollection.Name)]
    public class LibraryTests(ApiFactory factory)
    {
        [Fact]
        public async Task SameContent_IsNotUploadedAgain_DeletedBookDuplicate_IsReported_NotesAreDeleted()
        {
            var ct = TestContext.Current.CancellationToken;
            var client = factory.CreateClient();
            var folder = ImportClient.UniqueFolder("Copies");
            var title = "Copy test " + Guid.NewGuid().ToString("N")[..8];
            var content = TestFiles.UniquePdf(title);

            (await ImportClient.ImportAsync(client, [new($"{folder}/a.pdf", content)], ct))
                .GetProperty("importedCount").GetInt32().Should().Be(1);

            // The same content in another category: reported, not uploaded, the book keeps one file.
            var uploads = factory.Storage.Uploads;
            var second = await ImportClient.ImportAsync(client, [new($"{folder}/sub/a-copy.pdf", content)], ct);
            second.GetProperty("alreadyInLibraryCount").GetInt32().Should().Be(1);
            factory.Storage.Uploads.Should().Be(uploads);
            var items = await ImportClient.ItemsAsync(client, second.GetProperty("id").GetInt64(), "AlreadyInLibrary", ct);
            items[0].GetProperty("message").GetString().Should().Contain($"{folder}/a.pdf");

            var book = await ImportClient.FindBookAsync(client, title, ct);
            book.GetProperty("files").GetArrayLength().Should().Be(1);
            var bookId = book.GetProperty("id").GetInt64();

            (await client.PostAsJsonAsync($"/api/v1/books/{bookId}/notes", new { type = "Note", text = "remember this" }, ct))
                .StatusCode.Should().Be(HttpStatusCode.Created);
            (await client.DeleteAsync($"/api/v1/books/{bookId}", ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                (await db.BookNotes.IgnoreQueryFilters().CountAsync(n => n.BookId == bookId, ct)).Should().Be(0);
            }

            // The same content again: skipped and reported with the deleted book's title.
            var third = await ImportClient.ImportAsync(client, [new($"{ImportClient.UniqueFolder("Other")}/again.pdf", content)], ct);
            third.GetProperty("deletedDuplicateCount").GetInt32().Should().Be(1);
            var deleted = await ImportClient.ItemsAsync(client, third.GetProperty("id").GetInt64(), "DeletedDuplicateSkipped", ct);
            deleted[0].GetProperty("message").GetString().Should().Contain(title);
        }

        [Fact]
        public async Task SameName_OtherContent_IsConflict_AndTheOriginalStays()
        {
            var ct = TestContext.Current.CancellationToken;
            var client = factory.CreateClient();
            var folder = ImportClient.UniqueFolder("Conflict");
            var title = "Conflict test " + Guid.NewGuid().ToString("N")[..8];
            await ImportClient.ImportAsync(client, [new($"{folder}/book.pdf", TestFiles.UniquePdf(title))], ct);

            var second = await ImportClient.ImportAsync(
                client, [new($"{folder}/book.pdf", TestFiles.UniquePdf(title + " v2"), LastModified: 1_770_000_000_000)], ct);

            second.GetProperty("conflictCount").GetInt32().Should().Be(1);
            (await ImportClient.FindBookAsync(client, title, ct)).GetProperty("files").GetArrayLength().Should().Be(1);
        }

        [Fact]
        public async Task Open_ReturnsStorageLink_Check_DetectsMissingObject()
        {
            var ct = TestContext.Current.CancellationToken;
            var client = factory.CreateClient();
            var folder = ImportClient.UniqueFolder("Open");
            var title = "Open test " + Guid.NewGuid().ToString("N")[..8];
            await ImportClient.ImportAsync(client, [new($"{folder}/Math/b.pdf", TestFiles.UniquePdf(title))], ct);

            var book = await ImportClient.FindBookAsync(client, title, ct);
            var fileId = book.GetProperty("files")[0].GetProperty("id").GetInt64();

            var opened = await client.PostAsync($"/api/v1/files/{fileId}/opened", null, ct);
            opened.StatusCode.Should().Be(HttpStatusCode.OK);
            (await opened.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("url").GetString().Should().StartWith("https://storage.test/d/");
            (await ImportClient.FindBookAsync(client, title, ct)).GetProperty("readingStatus").GetString().Should().Be("InProgress");

            // The object disappears from VStorage: the check marks the file Missing.
            factory.Storage.RemoveKey($"{folder}/Math/b.pdf");
            var check = await client.PostAsync("/api/v1/library/check", null, ct);
            check.StatusCode.Should().Be(HttpStatusCode.OK);
            (await check.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("filesMissing").GetInt32().Should().BeGreaterThanOrEqualTo(1);

            (await ImportClient.FindBookAsync(client, title, ct)).GetProperty("filesState").GetString().Should().Be("Missing");
            (await client.PostAsync($"/api/v1/files/{fileId}/opened", null, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
