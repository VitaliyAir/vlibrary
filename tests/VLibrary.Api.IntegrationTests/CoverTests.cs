using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using VLibrary.Infrastructure.Persistence;

namespace VLibrary.Api.IntegrationTests
{
    /// <summary>B1: covers made during import, served as JPEG, and generated later for files without one.</summary>
    [Collection(ApiCollection.Name)]
    public class CoverTests(ApiFactory factory)
    {
        private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];

        [Fact]
        public async Task ImportedPdf_HasCover_ServedAsCacheableJpeg()
        {
            var ct = TestContext.Current.CancellationToken;
            var client = factory.CreateClient();
            var title = "Cover pdf " + Guid.NewGuid().ToString("N")[..8];
            await ImportClient.ImportAsync(client, [new($"{ImportClient.UniqueFolder("Covers")}/a.pdf", TestFiles.UniquePdf(title))], ct);

            var book = await ImportClient.FindBookAsync(client, title, ct);
            var cover = book.GetProperty("cover");
            cover.ValueKind.Should().Be(JsonValueKind.Object);
            var fileId = cover.GetProperty("fileId").GetInt64();
            var version = cover.GetProperty("version").GetInt64();
            book.GetProperty("files")[0].GetProperty("coverVersion").GetInt64().Should().Be(version);

            var response = await client.GetAsync($"/api/v1/files/{fileId}/cover?v={version}", ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.Should().Be("image/jpeg");
            response.Headers.CacheControl!.ToString().Should().Contain("immutable");
            response.Headers.ETag.Should().NotBeNull();
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            bytes.Take(3).Should().Equal(JpegMagic);

            using var image = SKBitmap.Decode(bytes);
            image.Width.Should().BeLessThanOrEqualTo(300);
            image.Height.Should().BeLessThanOrEqualTo(450);
        }

        [Fact]
        public async Task ImportedEpub_UsesEmbeddedCoverImage()
        {
            var ct = TestContext.Current.CancellationToken;
            var client = factory.CreateClient();
            var title = "Cover epub " + Guid.NewGuid().ToString("N")[..8];
            var epub = TestFiles.Epub(title, "Epub Author", "", TestFiles.Png(SKColors.Red));

            await ImportClient.ImportAsync(client, [new($"{ImportClient.UniqueFolder("Covers")}/b.epub", epub)], ct);

            var book = await ImportClient.FindBookAsync(client, title, ct);
            var cover = book.GetProperty("cover");
            var bytes = await client.GetByteArrayAsync(
                $"/api/v1/files/{cover.GetProperty("fileId").GetInt64()}/cover?v={cover.GetProperty("version").GetInt64()}", ct);

            // The red 200 x 300 PNG, kept at its size (never scaled up) and stored as JPEG.
            using var image = SKBitmap.Decode(bytes);
            image.Width.Should().Be(200);
            image.Height.Should().Be(300);
            var pixel = image.GetPixel(100, 150);
            pixel.Red.Should().BeGreaterThan(200);
            pixel.Green.Should().BeLessThan(60);
        }

        [Fact]
        public async Task ImportedTxt_HasNoCover()
        {
            var ct = TestContext.Current.CancellationToken;
            var client = factory.CreateClient();
            var name = "Plain text " + Guid.NewGuid().ToString("N")[..8];
            var text = Encoding.UTF8.GetBytes(name + " — a book without pictures.");

            await ImportClient.ImportAsync(client, [new($"{ImportClient.UniqueFolder("Covers")}/{name}.txt", text)], ct);

            var book = await ImportClient.FindBookAsync(client, name, ct);
            book.GetProperty("cover").ValueKind.Should().Be(JsonValueKind.Null);
            var fileId = book.GetProperty("files")[0].GetProperty("id").GetInt64();
            (await client.GetAsync($"/api/v1/files/{fileId}/cover", ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Generate_MakesMissingCovers_From_Storage_And_PausesWhenStorageIsDown()
        {
            var ct = TestContext.Current.CancellationToken;
            var client = factory.CreateClient();
            var title = "Cover later " + Guid.NewGuid().ToString("N")[..8];
            await ImportClient.ImportAsync(client, [new($"{ImportClient.UniqueFolder("Covers")}/c.pdf", TestFiles.UniquePdf(title))], ct);
            var fileId = (await ImportClient.FindBookAsync(client, title, ct)).GetProperty("files")[0].GetProperty("id").GetInt64();

            // As if the file had been imported before covers existed.
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.FileCovers.Where(c => c.FileMetaId == fileId).ExecuteDeleteAsync(ct);
                await db.FileMetas.Where(f => f.Id == fileId)
                    .ExecuteUpdateAsync(s => s.SetProperty(f => f.CoverCheckedAt, (DateTime?)null), ct);
            }

            (await client.GetFromJsonAsync<JsonElement>("/api/v1/covers/status", ct))
                .GetProperty("pending").GetInt32().Should().BeGreaterThanOrEqualTo(1);

            factory.Storage.Unavailable = true;
            try
            {
                (await client.PostAsync("/api/v1/covers/generate?take=20", null, ct)).StatusCode
                    .Should().Be(HttpStatusCode.ServiceUnavailable);
            }
            finally
            {
                factory.Storage.Unavailable = false;
            }

            // Repeat like the browser does until nothing remains.
            for (var round = 0; round < 20; round++)
            {
                var result = await client.PostAsync("/api/v1/covers/generate?take=20", null, ct);
                result.StatusCode.Should().Be(HttpStatusCode.OK);
                if ((await result.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("remaining").GetInt32() == 0)
                {
                    break;
                }
            }

            var cover = (await ImportClient.FindBookAsync(client, title, ct)).GetProperty("cover");
            cover.GetProperty("fileId").GetInt64().Should().Be(fileId);
        }
    }
}
