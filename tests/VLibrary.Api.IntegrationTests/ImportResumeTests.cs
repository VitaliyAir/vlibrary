using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace VLibrary.Api.IntegrationTests
{
    /// <summary>Continuing a stopped import with the same files: nothing is counted or uploaded twice.</summary>
    [Collection(ApiCollection.Name)]
    public class ImportResumeTests(ApiFactory factory)
    {
        [Fact]
        public async Task AStoppedImport_IsContinued_WithoutCountingFilesTwice()
        {
            var ct = TestContext.Current.CancellationToken;
            var client = factory.CreateClient();
            var folder = ImportClient.UniqueFolder("Resume");
            var first = new UploadFile($"{folder}/a.pdf", TestFiles.UniquePdf("Resume a " + folder));
            var second = new UploadFile($"{folder}/b.pdf", TestFiles.UniquePdf("Resume b " + folder));
            var id = await ImportClient.CreateAsync(client, 2, ct);
            (await ImportClient.UploadAsync(client, id, first, ct)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.PostAsync($"/api/v1/imports/{id}/cancel", null, ct)).IsSuccessStatusCode.Should().BeTrue();

            using var resume = await client.PostAsJsonAsync($"/api/v1/imports/{id}/resume", new { totalFiles = 2 }, ct);

            resume.StatusCode.Should().Be(HttpStatusCode.OK);
            (await resume.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("status").GetString().Should().Be("Running");
            using var precheck = await client.PostAsJsonAsync(
                $"/api/v1/imports/{id}/precheck",
                new { files = new[] { first, second }.Select(f => new { relativePath = f.RelativePath, size = f.Content.LongLength, lastModified = f.LastModified }) },
                ct);
            (await precheck.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("skip").EnumerateArray()
                .Select(s => s.GetString()).Should().Equal(first.RelativePath);
            (await ImportClient.UploadAsync(client, id, second, ct)).StatusCode.Should().Be(HttpStatusCode.OK);
            // Sent again anyway: already reported by this import, not counted.
            (await ImportClient.UploadAsync(client, id, first, ct)).StatusCode.Should().Be(HttpStatusCode.OK);

            using var complete = await client.PostAsync($"/api/v1/imports/{id}/complete", null, ct);
            var done = await complete.Content.ReadFromJsonAsync<JsonElement>(ct);
            done.GetProperty("status").GetString().Should().Be("Completed");
            done.GetProperty("importedCount").GetInt32().Should().Be(2);
            done.GetProperty("unchangedCount").GetInt32().Should().Be(0);
            done.GetProperty("processedFiles").GetInt32().Should().Be(2);
        }

        [Fact]
        public async Task AContinuedImport_DoesNotAskForFilesItAlreadyReported()
        {
            var ct = TestContext.Current.CancellationToken;
            var client = factory.CreateClient();
            var folder = ImportClient.UniqueFolder("Reported");
            var content = TestFiles.UniquePdf("Reported " + folder);
            await ImportClient.ImportAsync(client, [new($"{folder}/x.pdf", content)], ct);
            var copy = new UploadFile($"{folder}/Copies/x-copy.pdf", content); // same content elsewhere -> already in library
            var id = await ImportClient.CreateAsync(client, 1, ct);
            (await ImportClient.UploadAsync(client, id, copy, ct)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.PostAsync($"/api/v1/imports/{id}/cancel", null, ct)).IsSuccessStatusCode.Should().BeTrue();
            (await client.PostAsJsonAsync($"/api/v1/imports/{id}/resume", new { totalFiles = 1 }, ct)).StatusCode.Should().Be(HttpStatusCode.OK);

            using var precheck = await client.PostAsJsonAsync($"/api/v1/imports/{id}/precheck",
                new { files = new[] { new { relativePath = copy.RelativePath, size = copy.Content.LongLength, lastModified = copy.LastModified } } }, ct);

            (await precheck.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("skip").EnumerateArray()
                .Select(s => s.GetString()).Should().Equal(copy.RelativePath);
        }

        [Fact]
        public async Task ACompletedImport_CannotBeContinued()
        {
            var ct = TestContext.Current.CancellationToken;
            var client = factory.CreateClient();
            var folder = ImportClient.UniqueFolder("Done");
            var result = await ImportClient.ImportAsync(client, [new($"{folder}/c.pdf", TestFiles.UniquePdf("Done c " + folder))], ct);

            using var resume = await client.PostAsJsonAsync($"/api/v1/imports/{result.GetProperty("id").GetInt64()}/resume", new { totalFiles = 1 }, ct);

            resume.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
    }
}
