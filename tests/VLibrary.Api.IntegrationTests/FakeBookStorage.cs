using System.Collections.Concurrent;
using System.Security.Cryptography;
using VLibrary.Application.Abstractions;
using VLibrary.Domain.Common;

namespace VLibrary.Api.IntegrationTests
{
    /// <summary>
    /// In-memory VStorage with the same key rules (same content at a key = no-op, other content = KeyTaken);
    /// keeps the content so covers can be generated from it later.
    /// One instance per test run; tests use unique titles and keys.
    /// </summary>
    public sealed class FakeBookStorage : IBookStorage
    {
        private readonly ConcurrentDictionary<Guid, (string Key, string Sha256, byte[] Data)> _objects = new();
        private int _uploads;

        /// <summary>Simulates a VStorage outage (503).</summary>
        public bool Unavailable { get; set; }

        public int Uploads => _uploads;

        public bool HasKey(string key) => _objects.Values.Any(o => o.Key == key);

        /// <summary>Simulates an object that disappeared from VStorage.</summary>
        public void RemoveKey(string key)
        {
            foreach (var entry in _objects.Where(o => o.Value.Key == key).ToList())
            {
                _objects.TryRemove(entry.Key, out _);
            }
        }

        public async Task<Result<StoredObjectInfo>> UploadAsync(
            string key, string fileName, Stream content, long length, CancellationToken cancellationToken)
        {
            if (Unavailable)
            {
                return StorageErrors.Unavailable;
            }

            Interlocked.Increment(ref _uploads);
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            var data = buffer.ToArray();
            var sha256 = Convert.ToHexStringLower(SHA256.HashData(data));
            var existing = _objects.FirstOrDefault(o => o.Value.Key == key);
            if (existing.Key != Guid.Empty)
            {
                return existing.Value.Sha256 == sha256
                    ? new StoredObjectInfo(existing.Key, key, Created: false)
                    : StorageErrors.KeyTaken;
            }

            var id = Guid.NewGuid();
            _objects[id] = (key, sha256, data);
            return new StoredObjectInfo(id, key, Created: true);
        }

        public Task<Result<Stream>> OpenReadAsync(Guid objectId, CancellationToken cancellationToken)
        {
            if (Unavailable)
            {
                return Task.FromResult<Result<Stream>>(StorageErrors.Unavailable);
            }

            return Task.FromResult<Result<Stream>>(_objects.TryGetValue(objectId, out var stored)
                ? new MemoryStream(stored.Data, writable: false)
                : StorageErrors.NotFound);
        }

        public Task<Result> DeleteAsync(Guid objectId, CancellationToken cancellationToken)
        {
            _objects.TryRemove(objectId, out _);
            return Task.FromResult(Result.Success());
        }

        public Task<Result<StorageRenameResult>> RenameAsync(IReadOnlyList<StorageRename> renames, CancellationToken cancellationToken)
        {
            if (Unavailable)
            {
                return Task.FromResult<Result<StorageRenameResult>>(StorageErrors.Unavailable);
            }

            // A new key must not belong to another object (all or nothing).
            var ids = renames.Select(r => r.ObjectId).ToHashSet();
            if (renames.Any(r => _objects.Any(o => !ids.Contains(o.Key) && o.Value.Key == r.Key)))
            {
                return Task.FromResult<Result<StorageRenameResult>>(StorageErrors.RenameConflict);
            }

            var renamed = 0;
            var missing = new List<Guid>();
            foreach (var rename in renames)
            {
                if (!_objects.TryGetValue(rename.ObjectId, out var stored))
                {
                    missing.Add(rename.ObjectId);
                }
                else if (stored.Key != rename.Key)
                {
                    _objects[rename.ObjectId] = (rename.Key, stored.Sha256, stored.Data);
                    renamed++;
                }
            }

            return Task.FromResult<Result<StorageRenameResult>>(new StorageRenameResult(renamed, missing));
        }

        public Task<Result<StorageDownloadLink>> CreateDownloadLinkAsync(Guid objectId, CancellationToken cancellationToken) =>
            Task.FromResult<Result<StorageDownloadLink>>(_objects.ContainsKey(objectId)
                ? new StorageDownloadLink($"https://storage.test/d/{objectId:N}", DateTimeOffset.UtcNow.AddMinutes(5))
                : StorageErrors.NotFound);

        public Task<Result<IReadOnlySet<Guid>>> ListObjectIdsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<Result<IReadOnlySet<Guid>>>(Unavailable
                ? StorageErrors.Unavailable
                : new HashSet<Guid>(_objects.Keys));
    }
}
