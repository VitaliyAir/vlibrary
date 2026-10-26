using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace VLibrary.Api.IntegrationTests
{
    /// <summary>Renaming a book file: VStorage key, original name at the next import, name rules.</summary>
    [Collection(ApiCollection.Name)]
    public class FileRenameTests(ApiFactory factory)
    {
        [Fact]
        public async Task Rename_KeepsTheExtension_AndAnImportOfTheOriginalName_IsUnchanged()
        {
            var ct = TestContext.Current.CancellationToken;
            var client = factory.CreateClient();
            var folder = ImportClient.UniqueFolder("Rename");
            var title = "Sky " + folder;
            var original = new UploadFile($"{folder}/TT_22AA.pdf", TestFiles.UniquePdf(title));
            await ImportClient.ImportAsync(client, [original], ct);
            var book = await ImportClient.FindBookAsync(client, title, ct);
            var bookId = book.GetProperty("id").GetInt64();
            var fileId = book.GetProperty("files")[0].GetProperty("id").GetInt64();

            using var response = await client.PutAsJsonAsync($"/api/v1/files/{fileId}/name", new { name = "Astronomy", useAsTitle = true }, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("fileName").GetString().Should().Be("Astronomy.pdf");
            factory.Storage.HasKey($"{folder}/Astronomy.pdf").Should().BeTrue();
            factory.Storage.HasKey($"{folder}/TT_22AA.pdf").Should().BeFalse();
            (await client.GetFromJsonAsync<JsonElement>($"/api/v1/books/{bookId}", ct)).GetProperty("title").GetString().Should().Be("Astronomy");

            // The disk folder still has TT_22AA.pdf: imported again, it is the same file.
            var uploads = factory.Storage.Uploads;
            var again = await ImportClient.ImportAsync(client, [original], ct);
            again.GetProperty("unchangedCount").GetInt32().Should().Be(1);
            again.GetProperty("importedCount").GetInt32().Should().Be(0);
            factory.Storage.Uploads.Should().Be(uploads);
        }

        [Fact]
        public async Task ATakenName_IgnoringCase_IsAConflict_AndAnInvalidName_IsRejected()
        {
            var ct = TestContext.Current.CancellationToken;
            var client = factory.CreateClient();
            var folder = ImportClient.UniqueFolder("Names");
            await ImportClient.ImportAsync(client,
            [
                new($"{folder}/a.pdf", TestFiles.UniquePdf("Names a " + folder)),
                new($"{folder}/b.pdf", TestFiles.UniquePdf("Names b " + folder))
            ], ct);
            var b = await ImportClient.FindBookAsync(client, "Names b " + folder, ct);
            var fileId = b.GetProperty("files")[0].GetProperty("id").GetInt64();

            (await client.PutAsJsonAsync($"/api/v1/files/{fileId}/name", new { name = "A", useAsTitle = false }, ct))
                .StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await client.PutAsJsonAsync($"/api/v1/files/{fileId}/name", new { name = "x:y", useAsTitle = false }, ct))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
            factory.Storage.HasKey($"{folder}/b.pdf").Should().BeTrue();
        }
    }
}
