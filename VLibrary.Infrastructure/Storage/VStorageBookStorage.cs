using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VLibrary.Application.Abstractions;
using VLibrary.Domain.Common;

namespace VLibrary.Infrastructure.Storage
{
    /// <summary>
    /// Book files in VStorageAPI v2: the user's bucket <see cref="VStorageOptions.BucketName"/>, private objects,
    /// bearer token from the token exchange. Bodies are streamed, never buffered in memory.
    /// </summary>
    internal sealed class VStorageBookStorage(
        HttpClient http,
        VStorageTokenProvider tokens,
        IMemoryCache cache,
        IOptions<VStorageOptions> options,
        ICurrentUser currentUser,
        ILogger<VStorageBookStorage> logger) : IBookStorage
    {
        private const int ListPageSize = 1000;
        private const int RenameChunkSize = 1000;
        private const int MaxLoggedBody = 500;

        private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".epub"] = "application/epub+zip",
            [".djvu"] = "image/vnd.djvu",
            [".djv"] = "image/vnd.djvu",
            [".doc"] = "application/msword",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".txt"] = "text/plain"
        };

        private sealed record BucketInfo(Guid Id, string Name, string? Role);

        private sealed record ObjectInfo(Guid Id, string Key);

        private sealed record ObjectListing(List<ObjectInfo> Objects, string? NextCursor);

        private sealed record DownloadLinkInfo(string Url, DateTimeOffset ExpiresAt);

        private sealed record RenameInfo(int Renamed, int Unchanged, List<Guid> Missing);

        private string BucketCacheKey => $"vstorage-bucket:{currentUser.UserId}";

        public async Task<Result<StoredObjectInfo>> UploadAsync(
            string key, string fileName, Stream content, long length, CancellationToken cancellationToken)
        {
            var access = await GetAccessAsync(cancellationToken);
            if (access.IsFailure)
            {
                return access.Error;
            }

            var (token, bucketId) = access.Value;
            try
            {
                // No precondition (FR-O6): same content at the key -> 200 with the existing object, other content -> 409.
                using var request = CreateRequest(
                    HttpMethod.Put, $"api/v1/buckets/{bucketId}/objects?key={Uri.EscapeDataString(key)}&visibility=private", token);
                var body = new StreamContent(content);
                body.Headers.ContentType = new MediaTypeHeaderValue(
                    ContentTypes.GetValueOrDefault(Path.GetExtension(fileName), "application/octet-stream"));
                body.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileNameStar = fileName };
                body.Headers.ContentLength = length;
                request.Content = body;

                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var stored = await response.Content.ReadFromJsonAsync<ObjectInfo>(cancellationToken);
                    return stored is { Id: var id } && id != Guid.Empty
                        ? new StoredObjectInfo(id, stored.Key, response.StatusCode == HttpStatusCode.Created)
                        : StorageErrors.Unavailable;
                }

                switch (response.StatusCode)
                {
                    case HttpStatusCode.Conflict:
                        return StorageErrors.KeyTaken;
                    case HttpStatusCode.RequestEntityTooLarge:
                        return StorageErrors.TooLarge;
                    case HttpStatusCode.NotFound:
                        // The bucket may have been deleted: look it up (or create it) again next time.
                        cache.Remove(BucketCacheKey);
                        break;
                }

                await LogFailureAsync("upload", key, response, cancellationToken);
                return StorageErrors.Unavailable;
            }
            catch (Exception exception) when (IsTransport(exception, cancellationToken))
            {
                logger.LogError(exception, "VStorage upload of {Key} failed", key);
                return StorageErrors.Unavailable;
            }
        }

        public async Task<Result<Stream>> OpenReadAsync(Guid objectId, CancellationToken cancellationToken)
        {
            var token = await tokens.GetTokenAsync(cancellationToken);
            if (token.IsFailure)
            {
                return token.Error;
            }

            try
            {
                using var request = CreateRequest(HttpMethod.Get, $"api/v1/objects/{objectId}/content", token.Value);
                var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    // The response is released together with the stream the caller disposes.
                    return await response.Content.ReadAsStreamAsync(cancellationToken);
                }

                using (response)
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return StorageErrors.NotFound;
                    }

                    await LogFailureAsync("download", objectId.ToString(), response, cancellationToken);
                    return StorageErrors.Unavailable;
                }
            }
            catch (Exception exception) when (IsTransport(exception, cancellationToken))
            {
                logger.LogError(exception, "VStorage download of {ObjectId} failed", objectId);
                return StorageErrors.Unavailable;
            }
        }

        public async Task<Result> DeleteAsync(Guid objectId, CancellationToken cancellationToken)
        {
            var token = await tokens.GetTokenAsync(cancellationToken);
            if (token.IsFailure)
            {
                return token.Error;
            }

            try
            {
                using var request = CreateRequest(HttpMethod.Delete, $"api/v1/objects/{objectId}", token.Value);
                using var response = await http.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
                {
                    return Result.Success();
                }

                await LogFailureAsync("delete", objectId.ToString(), response, cancellationToken);
                return StorageErrors.Unavailable;
            }
            catch (Exception exception) when (IsTransport(exception, cancellationToken))
            {
                logger.LogError(exception, "VStorage delete of {ObjectId} failed", objectId);
                return StorageErrors.Unavailable;
            }
        }

        public async Task<Result<StorageRenameResult>> RenameAsync(IReadOnlyList<StorageRename> renames, CancellationToken cancellationToken)
        {
            var access = await GetAccessAsync(cancellationToken);
            if (access.IsFailure)
            {
                return access.Error;
            }

            var (token, bucketId) = access.Value;
            var renamed = 0;
            var missing = new List<Guid>();
            try
            {
                foreach (var chunk in renames.Chunk(RenameChunkSize))
                {
                    using var request = CreateRequest(HttpMethod.Post, $"api/v1/buckets/{bucketId}/objects/rename", token);
                    request.Content = JsonContent.Create(new { items = chunk.Select(r => new { objectId = r.ObjectId, key = r.Key }) });
                    using var response = await http.SendAsync(request, cancellationToken);
                    if (response.IsSuccessStatusCode && await response.Content.ReadFromJsonAsync<RenameInfo>(cancellationToken) is { } info)
                    {
                        renamed += info.Renamed;
                        missing.AddRange(info.Missing);
                        continue;
                    }

                    await LogFailureAsync("rename", $"{chunk.Length} object(s)", response, cancellationToken);
                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.Conflict:
                            return StorageErrors.RenameConflict;
                        case HttpStatusCode.NotFound:
                            cache.Remove(BucketCacheKey);
                            break;
                    }

                    return StorageErrors.Unavailable;
                }
            }
            catch (Exception exception) when (IsTransport(exception, cancellationToken))
            {
                logger.LogError(exception, "VStorage rename of {Count} object(s) failed", renames.Count);
                return StorageErrors.Unavailable;
            }

            return new StorageRenameResult(renamed, missing);
        }

        public async Task<Result<StorageDownloadLink>> CreateDownloadLinkAsync(Guid objectId, CancellationToken cancellationToken)
        {
            var token = await tokens.GetTokenAsync(cancellationToken);
            if (token.IsFailure)
            {
                return token.Error;
            }

            try
            {
                using var request = CreateRequest(HttpMethod.Post, $"api/v1/objects/{objectId}/download-link", token.Value);
                request.Content = JsonContent.Create(new { expiresInSeconds = options.Value.DownloadLinkSeconds, disposition = "inline" });
                using var response = await http.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var link = await response.Content.ReadFromJsonAsync<DownloadLinkInfo>(cancellationToken);
                    return link is { Url.Length: > 0 }
                        ? new StorageDownloadLink(link.Url, link.ExpiresAt)
                        : StorageErrors.Unavailable;
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return StorageErrors.NotFound;
                }

                await LogFailureAsync("download-link", objectId.ToString(), response, cancellationToken);
                return StorageErrors.Unavailable;
            }
            catch (Exception exception) when (IsTransport(exception, cancellationToken))
            {
                logger.LogError(exception, "VStorage download link for {ObjectId} failed", objectId);
                return StorageErrors.Unavailable;
            }
        }

        public async Task<Result<IReadOnlySet<Guid>>> ListObjectIdsAsync(CancellationToken cancellationToken)
        {
            var access = await GetAccessAsync(cancellationToken);
            if (access.IsFailure)
            {
                return access.Error;
            }

            var (token, bucketId) = access.Value;
            var ids = new HashSet<Guid>();
            string? cursor = null;
            try
            {
                do
                {
                    var url = $"api/v1/buckets/{bucketId}/objects?limit={ListPageSize}"
                        + (cursor == null ? string.Empty : "&cursor=" + Uri.EscapeDataString(cursor));
                    using var request = CreateRequest(HttpMethod.Get, url, token);
                    using var response = await http.SendAsync(request, cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        await LogFailureAsync("list", bucketId.ToString(), response, cancellationToken);
                        return StorageErrors.Unavailable;
                    }

                    var page = await response.Content.ReadFromJsonAsync<ObjectListing>(cancellationToken);
                    if (page == null)
                    {
                        return StorageErrors.Unavailable;
                    }

                    ids.UnionWith(page.Objects.Select(o => o.Id));
                    cursor = page.NextCursor;
                }
                while (!string.IsNullOrEmpty(cursor));

                return ids;
            }
            catch (Exception exception) when (IsTransport(exception, cancellationToken))
            {
                logger.LogError(exception, "VStorage listing of bucket {BucketId} failed", bucketId);
                return StorageErrors.Unavailable;
            }
        }

        private async Task<Result<(string Token, Guid BucketId)>> GetAccessAsync(CancellationToken cancellationToken)
        {
            var token = await tokens.GetTokenAsync(cancellationToken);
            if (token.IsFailure)
            {
                return token.Error;
            }

            try
            {
                var bucket = await ResolveBucketAsync(token.Value, cancellationToken);
                return bucket.IsFailure ? bucket.Error : (token.Value, bucket.Value);
            }
            catch (Exception exception) when (IsTransport(exception, cancellationToken))
            {
                logger.LogError(exception, "VStorage bucket lookup failed");
                return StorageErrors.Unavailable;
            }
        }

        /// <summary>The user's library bucket: found by name among the user's buckets, otherwise created.</summary>
        private async Task<Result<Guid>> ResolveBucketAsync(string token, CancellationToken cancellationToken)
        {
            if (cache.TryGetValue(BucketCacheKey, out Guid cached) && cached != Guid.Empty)
            {
                return cached;
            }

            var name = options.Value.BucketName;
            using (var list = CreateRequest(HttpMethod.Get, "api/v1/buckets", token))
            using (var response = await http.SendAsync(list, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                {
                    await LogFailureAsync("list-buckets", name, response, cancellationToken);
                    return StorageErrors.Unavailable;
                }

                var buckets = await response.Content.ReadFromJsonAsync<List<BucketInfo>>(cancellationToken) ?? [];
                var match = buckets
                    .Where(b => string.Equals(b.Name, name, StringComparison.Ordinal))
                    .OrderByDescending(b => string.Equals(b.Role, "owner", StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();
                if (match != null)
                {
                    cache.Set(BucketCacheKey, match.Id, TimeSpan.FromHours(1));
                    return match.Id;
                }
            }

            using var create = CreateRequest(HttpMethod.Post, "api/v1/buckets", token);
            create.Content = JsonContent.Create(new { name });
            using var created = await http.SendAsync(create, cancellationToken);
            if (created.StatusCode == HttpStatusCode.Created
                && await created.Content.ReadFromJsonAsync<BucketInfo>(cancellationToken) is { } bucket)
            {
                logger.LogInformation("Created the VStorage bucket {Bucket} ({BucketId}) for {User}", name, bucket.Id, currentUser.UserName);
                cache.Set(BucketCacheKey, bucket.Id, TimeSpan.FromHours(1));
                return bucket.Id;
            }

            if (created.StatusCode == HttpStatusCode.Forbidden)
            {
                logger.LogWarning(
                    "User {User} may not create the VStorage bucket {Bucket}: the client role bucket-creator of client 'vstorage' is missing",
                    currentUser.UserName, name);
                return StorageErrors.Forbidden;
            }

            await LogFailureAsync("create-bucket", name, created, cancellationToken);
            return StorageErrors.Unavailable;
        }

        private static HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl, string token)
        {
            var request = new HttpRequestMessage(method, relativeUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return request;
        }

        private async Task LogFailureAsync(string operation, string subject, HttpResponseMessage response, CancellationToken cancellationToken)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning(
                "VStorage {Operation} {Subject} returned {Status}: {Body}",
                operation, subject, (int)response.StatusCode, body.Length > MaxLoggedBody ? body[..MaxLoggedBody] : body);
        }

        // Network failures, unreadable answers and storage timeouts = "unavailable"; a cancelled request is passed on.
        private static bool IsTransport(Exception exception, CancellationToken cancellationToken) =>
            exception is HttpRequestException or JsonException
            || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested);
    }
}
