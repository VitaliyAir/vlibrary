using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace VLibrary.Api.IntegrationTests
{
    [Collection(ApiCollection.Name)]
    public class ImportFlowTests(ApiFactory factory)
    {
        private const string Isbn = "9780306406157";

        [Fact]
        public async Task Health_AllMigrationsApplied()
        {
            var client = factory.CreateClient();

            var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain("Healthy");
        }

        [Fact]
        public async Task Import_SameBookTwice_SecondFileGoesToReview_CategoriesAreCreated_RerunUploadsNothing()
        {
            var ct = TestContext.Current.CancellationToken;
            var client = factory.CreateClient();
            var library = ImportClient.UniqueFolder("Library");
            UploadFile[] files =
            [
                new($"{library}/Math/a.pdf", TestFiles.Pdf("War and Peace", "Leo Tolstoy", $"ISBN {Isbn}")),
                new($"{library}/Math/Old/b.epub", TestFiles.Epub("War and Peace", "Tolstoy, Leo", Isbn))
            ];

            var batch = await ImportClient.ImportAsync(client, files, ct);
            batch.GetProperty("status").GetString().Should().Be("Completed");
            batch.GetProperty("importedCount").GetInt32().Should().Be(1);
            batch.GetProperty("pendingReviewCount").GetInt32().Should().Be(1);
            factory.Storage.HasKey($"{library}/Math/a.pdf").Should().BeTrue();
            factory.Storage.HasKey($"{library}/Math/Old/b.epub").Should().BeTrue();

            var categories = await client.GetFromJsonAsync<JsonElement>("/api/v1/categories", ct);
            var paths = categories.EnumerateArray().Select(c => c.GetProperty("path").GetString()).ToList();
            paths.Should().Contain([library, $"{library}/Math", $"{library}/Math/Old"]);
            categories.EnumerateArray().Single(c => c.GetProperty("path").GetString() == library)
                .GetProperty("totalFiles").GetInt32().Should().Be(2);

            var duplicates = await client.GetFromJsonAsync<JsonElement>("/api/v1/duplicates", ct);
            var pending = duplicates.EnumerateArray().Single(d => d.GetProperty("libraryPath").GetString()!.StartsWith(library));
            pending.GetProperty("libraryPath").GetString().Should().Be($"{library}/Math/Old/b.epub");
            var candidate = pending.GetProperty("candidates").EnumerateArray().Single();
            candidate.GetProperty("reason").GetString().Should().Be("IsbnMatch");

            var link = await client.PostAsJsonAsync(
                $"/api/v1/duplicates/{pending.GetProperty("fileMetaId").GetInt64()}/link",
                new { bookId = candidate.GetProperty("bookId").GetInt64() },
                ct);
            link.StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await client.GetFromJsonAsync<JsonElement>("/api/v1/duplicates", ct)).EnumerateArray()
                .Should().NotContain(d => d.GetProperty("libraryPath").GetString()!.StartsWith(library));

            // A second run of the same folder: the precheck skips both files, nothing is uploaded again.
            var uploadsBefore = factory.Storage.Uploads;
            var rerun = await ImportClient.ImportAsync(client, files, ct);
            rerun.GetProperty("unchangedCount").GetInt32().Should().Be(2);
            factory.Storage.Uploads.Should().Be(uploadsBefore);
        }

        [Fact]
        public async Task Import_IntoParentCategory_BuildsPathUnderIt()
        {
            var ct = TestContext.Current.CancellationToken;
            var client = factory.CreateClient();
            var parentName = ImportClient.UniqueFolder("Parent");
            var title = "Child test " + Guid.NewGuid().ToString("N")[..8];
            await ImportClient.ImportAsync(client, [new($"{parentName}/seed.pdf", TestFiles.UniquePdf("Seed " + parentName))], ct);
            var parentId = (await client.GetFromJsonAsync<JsonElement>("/api/v1/categories", ct)).EnumerateArray()
                .Single(c => c.GetProperty("path").GetString() == parentName).GetProperty("id").GetInt64();

            await ImportClient.ImportAsync(client, [new("Math/book.pdf", TestFiles.UniquePdf(title))], ct, parentId);

            var book = await ImportClient.FindBookAsync(client, title, ct);
            book.GetProperty("files")[0].GetProperty("libraryPath").GetString().Should().Be($"{parentName}/Math/book.pdf");
        }

        [Fact]
        public async Task Upload_Rejected_FileIsReportedAsFailed()
        {
            var ct = TestContext.Current.CancellationToken;
            var client = factory.CreateClient();
            var id = await ImportClient.CreateAsync(client, 2, ct);

            // No folder and no parent category: the file would have no category.
            var noCategory = await ImportClient.UploadAsync(client, id, new UploadFile("loose.pdf", TestFiles.UniquePdf("Loose")), ct);
            (await noCategory.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("outcome").GetString().Should().Be("Failed");

            var unsupported = await ImportClient.UploadAsync(client, id, new UploadFile("Misc/readme.md", [1, 2, 3]), ct);
            (await unsupported.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("outcome").GetString().Should().Be("Failed");

            var notMultipart = await client.PostAsJsonAsync($"/api/v1/imports/{id}/files", new { relativePath = "x.pdf" }, ct);
            notMultipart.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Upload_StorageUnavailable_Is503_AndNothingIsRecorded()
        {
            var ct = TestContext.Current.CancellationToken;
            var client = factory.CreateClient();
            var folder = ImportClient.UniqueFolder("Outage");
            var file = new UploadFile($"{folder}/book.pdf", TestFiles.UniquePdf("Outage " + folder));
            var id = await ImportClient.CreateAsync(client, 1, ct);

            factory.Storage.Unavailable = true;
            try
            {
                (await ImportClient.UploadAsync(client, id, file, ct)).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            }
            finally
            {
                factory.Storage.Unavailable = false;
            }

            var batch = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/imports/{id}", ct)).GetProperty("batch");
            batch.GetProperty("processedFiles").GetInt32().Should().Be(0);

            // Retry of the same file after the outage.
            var retry = await ImportClient.UploadAsync(client, id, file, ct);
            (await retry.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("outcome").GetString().Should().Be("Imported");
        }
    }
}
