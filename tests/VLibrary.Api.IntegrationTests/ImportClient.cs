using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace VLibrary.Api.IntegrationTests
{
    /// <param name="RelativePath">Path from the category start, e.g. "Library/Math/a.pdf".</param>
    /// <param name="LastModified">Unix milliseconds, as File.lastModified in the browser.</param>
    internal sealed record UploadFile(string RelativePath, byte[] Content, long LastModified = 1_760_000_000_000);

    /// <summary>Drives an import the way the browser does: create -> precheck -> one upload per file -> complete.</summary>
    internal static class ImportClient
    {
        public static async Task<JsonElement> ImportAsync(
            HttpClient client, IReadOnlyList<UploadFile> files, CancellationToken ct, long? parentCategoryId = null)
        {
            var id = await CreateAsync(client, files.Count, ct, parentCategoryId);

            var precheck = await client.PostAsJsonAsync(
                $"/api/v1/imports/{id}/precheck",
                new { files = files.Select(f => new { relativePath = f.RelativePath, size = f.Content.LongLength, lastModified = f.LastModified }) },
                ct);
            precheck.StatusCode.Should().Be(HttpStatusCode.OK);
            var skip = (await precheck.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("skip")
                .EnumerateArray().Select(s => s.GetString()).ToHashSet();

            foreach (var file in files.Where(f => !skip.Contains(f.RelativePath)))
            {
                var upload = await UploadAsync(client, id, file, ct);
                upload.StatusCode.Should().Be(HttpStatusCode.OK);
            }

            var complete = await client.PostAsync($"/api/v1/imports/{id}/complete", null, ct);
            complete.StatusCode.Should().Be(HttpStatusCode.OK);
            return await complete.Content.ReadFromJsonAsync<JsonElement>(ct);
        }

        public static async Task<long> CreateAsync(HttpClient client, int totalFiles, CancellationToken ct, long? parentCategoryId = null)
        {
            var create = await client.PostAsJsonAsync(
                "/api/v1/imports", new { sourceName = "test", totalFiles, parentCategoryId }, ct);
            create.StatusCode.Should().Be(HttpStatusCode.Created);
            return (await create.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetInt64();
        }

        public static Task<HttpResponseMessage> UploadAsync(HttpClient client, long importId, UploadFile file, CancellationToken ct)
        {
            var form = new MultipartFormDataContent
            {
                { new StringContent(file.RelativePath), "relativePath" },
                { new StringContent(file.LastModified.ToString()), "lastModified" }
            };
            var content = new ByteArrayContent(file.Content);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(content, "file", Path.GetFileName(file.RelativePath));
            return client.PostAsync($"/api/v1/imports/{importId}/files", form, ct);
        }

        public static async Task<JsonElement> ItemsAsync(HttpClient client, long importId, string outcome, CancellationToken ct) =>
            (await client.GetFromJsonAsync<JsonElement>($"/api/v1/imports/{importId}?outcome={outcome}", ct))
                .GetProperty("items").GetProperty("items");

        public static async Task<JsonElement> FindBookAsync(HttpClient client, string title, CancellationToken ct)
        {
            var page = await client.GetFromJsonAsync<JsonElement>($"/api/v1/books?q={Uri.EscapeDataString(title)}", ct);
            return page.GetProperty("items").EnumerateArray().Single();
        }

        /// <summary>A unique folder name, so tests sharing one database do not see each other's categories.</summary>
        public static string UniqueFolder(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 9)];
    }
}
