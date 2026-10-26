using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace VLibrary.Api.IntegrationTests
{
    /// <summary>Renaming, moving and merging categories; VStorage keys follow; old names lead imports to the current place.</summary>
    [Collection(ApiCollection.Name)]
    public class CategoryEditTests(ApiFactory factory)
    {
        private sealed record CategoryRef(long Id, long? ParentId, JsonElement Json);

        [Fact]
        public async Task Rename_RepathsSubcategories_AndRenamesVStorageKeys()
        {
            var ct = TestContext.Current.CancellationToken;
            var client = factory.CreateClient();
            var folder = ImportClient.UniqueFolder("Rename");
            await ImportClient.ImportAsync(client, [new($"{folder}/Physcs/Classic/a.pdf", TestFiles.UniquePdf("Rename " + folder))], ct);
            var physcs = await FindAsync(client, $"{folder}/Physcs", ct);

            using var response = await client.PutAsJsonAsync($"/api/v1/categories/{physcs!.Id}", new { name = "Physics", parentId = physcs.ParentId }, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var change = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            change.GetProperty("path").GetString().Should().Be($"{folder}/Physics");
            change.GetProperty("filesMoved").GetInt32().Should().Be(1);
            (await FindAsync(client, $"{folder}/Physics/Classic", ct)).Should().NotBeNull();
            (await FindAsync(client, $"{folder}/Physcs", ct)).Should().BeNull();
            factory.Storage.HasKey($"{folder}/Physics/Classic/a.pdf").Should().BeTrue();
            factory.Storage.HasKey($"{folder}/Physcs/Classic/a.pdf").Should().BeFalse();
        }

        [Fact]
        public async Task ImportFromTheOldFolder_FollowsTheRename()
        {
            var ct = TestContext.Current.CancellationToken;
            var client = factory.CreateClient();
            var folder = ImportClient.UniqueFolder("Redirect");
            var known = new UploadFile($"{folder}/Physcs/a.pdf", TestFiles.UniquePdf("Redirect a " + folder));
            await ImportClient.ImportAsync(client, [known], ct);
            var physcs = await FindAsync(client, $"{folder}/Physcs", ct);
            (await client.PutAsJsonAsync($"/api/v1/categories/{physcs!.Id}", new { name = "Physics", parentId = physcs.ParentId }, ct))
                .EnsureSuccessStatusCode();

            var second = await ImportClient.ImportAsync(
                client, [known, new($"{folder}/Physcs/b.pdf", TestFiles.UniquePdf("Redirect b " + folder))], ct);

            second.GetProperty("unchangedCount").GetInt32().Should().Be(1);
            second.GetProperty("importedCount").GetInt32().Should().Be(1);
            factory.Storage.HasKey($"{folder}/Physics/b.pdf").Should().BeTrue();
            (await FindAsync(client, $"{folder}/Physcs", ct)).Should().BeNull();
            var physics = await FindAsync(client, $"{folder}/Physics", ct);
            physics!.Json.GetProperty("redirects")[0].GetProperty("path").GetString().Should().Be($"{folder}/Physcs");
        }

        [Fact]
        public async Task MoveOntoSameName_Merges_OldPlaceLeadsToTarget_AndAFileNameClash_ChangesNothing()
        {
            var ct = TestContext.Current.CancellationToken;
            var client = factory.CreateClient();
            var folder = ImportClient.UniqueFolder("Merge");
            await ImportClient.ImportAsync(client,
            [
                new($"{folder}/A/Physics/x.pdf", TestFiles.UniquePdf("Merge x " + folder)),
                new($"{folder}/A/Physics/Sub/y.pdf", TestFiles.UniquePdf("Merge y " + folder)),
                new($"{folder}/B/Physics/z.pdf", TestFiles.UniquePdf("Merge z " + folder))
            ], ct);
            var source = await FindAsync(client, $"{folder}/A/Physics", ct);
            var b = await FindAsync(client, $"{folder}/B", ct);

            using var merged = await client.PutAsJsonAsync($"/api/v1/categories/{source!.Id}", new { name = "Physics", parentId = b!.Id }, ct);

            merged.StatusCode.Should().Be(HttpStatusCode.OK);
            (await merged.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("merged").GetBoolean().Should().BeTrue();
            (await FindAsync(client, $"{folder}/A/Physics", ct)).Should().BeNull();
            (await FindAsync(client, $"{folder}/B/Physics/Sub", ct)).Should().NotBeNull();
            factory.Storage.HasKey($"{folder}/B/Physics/x.pdf").Should().BeTrue();
            factory.Storage.HasKey($"{folder}/B/Physics/Sub/y.pdf").Should().BeTrue();

            // The old place leads to the merge target.
            await ImportClient.ImportAsync(client, [new($"{folder}/A/Physics/w.pdf", TestFiles.UniquePdf("Merge w " + folder))], ct);
            factory.Storage.HasKey($"{folder}/B/Physics/w.pdf").Should().BeTrue();

            // Another z.pdf (other content) cannot join B/Physics: 409 and nothing moves.
            await ImportClient.ImportAsync(client, [new($"{folder}/C/Physics/z.pdf", TestFiles.UniquePdf("Merge z2 " + folder))], ct);
            var clash = await FindAsync(client, $"{folder}/C/Physics", ct);
            using var refused = await client.PutAsJsonAsync($"/api/v1/categories/{clash!.Id}", new { name = "Physics", parentId = b.Id }, ct);
            refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
            factory.Storage.HasKey($"{folder}/C/Physics/z.pdf").Should().BeTrue();
        }

        [Fact]
        public async Task RenamingBack_TakesTheNameFromTheRedirect()
        {
            var ct = TestContext.Current.CancellationToken;
            var client = factory.CreateClient();
            var folder = ImportClient.UniqueFolder("Back");
            await ImportClient.ImportAsync(client, [new($"{folder}/Physcs/a.pdf", TestFiles.UniquePdf("Back " + folder))], ct);
            var physcs = await FindAsync(client, $"{folder}/Physcs", ct);

            (await client.PutAsJsonAsync($"/api/v1/categories/{physcs!.Id}", new { name = "Physics", parentId = physcs.ParentId }, ct))
                .EnsureSuccessStatusCode();
            (await client.PutAsJsonAsync($"/api/v1/categories/{physcs.Id}", new { name = "Physcs", parentId = physcs.ParentId }, ct))
                .EnsureSuccessStatusCode();

            var back = await FindAsync(client, $"{folder}/Physcs", ct);
            back!.Id.Should().Be(physcs.Id);
            back.Json.GetProperty("redirects")[0].GetProperty("path").GetString().Should().Be($"{folder}/Physics");
            factory.Storage.HasKey($"{folder}/Physcs/a.pdf").Should().BeTrue();
        }

        [Fact]
        public async Task OldFolderOfADeletedCategory_IsCreatedAgain_AndReported()
        {
            var ct = TestContext.Current.CancellationToken;
            var client = factory.CreateClient();
            var folder = ImportClient.UniqueFolder("Revive");
            await ImportClient.ImportAsync(client, [new($"{folder}/Keep/a.pdf", TestFiles.UniquePdf("Revive a " + folder))], ct);
            var root = await FindAsync(client, folder, ct);
            using var created = await client.PostAsJsonAsync("/api/v1/categories", new { name = "Old", parentId = root!.Id }, ct);
            var id = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetInt64();
            (await client.PutAsJsonAsync($"/api/v1/categories/{id}", new { name = "New", parentId = root.Id }, ct)).EnsureSuccessStatusCode();
            (await client.DeleteAsync($"/api/v1/categories/{id}", ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

            var import = await ImportClient.ImportAsync(client, [new($"{folder}/Old/b.pdf", TestFiles.UniquePdf("Revive b " + folder))], ct);

            import.GetProperty("importedCount").GetInt32().Should().Be(1);
            (await FindAsync(client, $"{folder}/Old", ct)).Should().NotBeNull();
            var items = await ImportClient.ItemsAsync(client, import.GetProperty("id").GetInt64(), "Imported", ct);
            items[0].GetProperty("message").GetString().Should().Contain("created again");
        }

        [Fact]
        public async Task MoveIntoOwnSubcategory_IsRejected_AndOnlyEmptyCategoriesAreDeleted()
        {
            var ct = TestContext.Current.CancellationToken;
            var client = factory.CreateClient();
            var folder = ImportClient.UniqueFolder("Tree");
            await ImportClient.ImportAsync(client, [new($"{folder}/P/Q/a.pdf", TestFiles.UniquePdf("Tree " + folder))], ct);
            var p = await FindAsync(client, $"{folder}/P", ct);
            var q = await FindAsync(client, $"{folder}/P/Q", ct);

            (await client.PutAsJsonAsync($"/api/v1/categories/{p!.Id}", new { name = "P", parentId = q!.Id }, ct))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);

            using var created = await client.PostAsJsonAsync("/api/v1/categories", new { name = "Empty", parentId = p.Id }, ct);
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            var emptyId = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetInt64();

            (await client.DeleteAsync($"/api/v1/categories/{p.Id}", ct)).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await client.DeleteAsync($"/api/v1/categories/{emptyId}", ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        private static async Task<CategoryRef?> FindAsync(HttpClient client, string path, CancellationToken ct)
        {
            var all = await client.GetFromJsonAsync<JsonElement>("/api/v1/categories", ct);
            return all.EnumerateArray()
                .Where(c => c.GetProperty("path").GetString() == path)
                .Select(c => new CategoryRef(
                    c.GetProperty("id").GetInt64(),
                    c.GetProperty("parentId").ValueKind == JsonValueKind.Null ? null : c.GetProperty("parentId").GetInt64(),
                    c))
                .FirstOrDefault();
        }
    }
}
