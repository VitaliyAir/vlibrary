using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace VLibrary.Api.IntegrationTests
{
    /// <summary>The book card's author rows: name parts in the details, "Last, First Middle" saved back.</summary>
    [Collection(ApiCollection.Name)]
    public class BookCardTests(ApiFactory factory)
    {
        [Fact]
        public async Task Authors_AreReturnedInParts_AndAFullerNameCompletesTheSameAuthor()
        {
            var ct = TestContext.Current.CancellationToken;
            var client = factory.CreateClient();
            var last = ImportClient.UniqueFolder("Author");
            var title = "Card " + last;
            using var created = await client.PostAsJsonAsync("/api/v1/books", new { title, authors = new[] { $"{last}, L.N." } }, ct);
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            var id = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetInt64();
            var before = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/books/{id}", ct)).GetProperty("authors")[0];
            before.GetProperty("initials").GetString().Should().Be("L.N.");

            using var updated = await client.PutAsJsonAsync($"/api/v1/books/{id}",
                new { title, authors = new[] { $"{last}, Lev Nikolaevich", $"Second{last}, Anna" } }, ct);

            updated.IsSuccessStatusCode.Should().BeTrue();
            var authors = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/books/{id}", ct)).GetProperty("authors");
            authors.GetArrayLength().Should().Be(2);
            authors[0].GetProperty("id").GetInt64().Should().Be(before.GetProperty("id").GetInt64());
            authors[0].GetProperty("lastName").GetString().Should().Be(last);
            authors[0].GetProperty("firstName").GetString().Should().Be("Lev");
            authors[0].GetProperty("middleName").GetString().Should().Be("Nikolaevich");
            authors[1].GetProperty("firstName").GetString().Should().Be("Anna");
        }
    }
}
