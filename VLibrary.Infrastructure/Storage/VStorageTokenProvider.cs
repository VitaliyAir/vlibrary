using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VLibrary.Application.Abstractions;
using VLibrary.Domain.Common;

namespace VLibrary.Infrastructure.Storage
{
    /// <summary>
    /// Exchanges the current user's access token for a VStorage token (aud = vstorage, Keycloak Standard Token Exchange);
    /// cached per user until exp - 30 s. Same as VGoalsPlanner.
    /// </summary>
    internal sealed class VStorageTokenProvider(
        HttpClient http,
        IMemoryCache cache,
        IOptions<VStorageOptions> options,
        ICurrentUser currentUser,
        ILogger<VStorageTokenProvider> logger)
    {
        private const int SafetySeconds = 30;
        private const int MaxLoggedBody = 500;

        public async Task<Result<string>> GetTokenAsync(CancellationToken cancellationToken)
        {
            var settings = options.Value;
            if (!settings.IsConfigured)
            {
                logger.LogWarning("VStorage is not configured (VStorage:BaseUrl, TokenEndpoint, ClientId, ClientSecret)");
                return StorageErrors.Unavailable;
            }

            var subjectToken = currentUser.AccessToken;
            if (string.IsNullOrEmpty(subjectToken) || string.IsNullOrEmpty(currentUser.UserId))
            {
                return StorageErrors.NoUserToken;
            }

            var cacheKey = $"vstorage-token:{currentUser.UserId}";
            if (cache.TryGetValue(cacheKey, out string? cached) && !string.IsNullOrEmpty(cached))
            {
                return cached;
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, settings.TokenEndpoint)
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["grant_type"] = "urn:ietf:params:oauth:grant-type:token-exchange",
                        ["client_id"] = settings.ClientId,
                        ["client_secret"] = settings.ClientSecret,
                        ["subject_token"] = subjectToken,
                        ["subject_token_type"] = "urn:ietf:params:oauth:token-type:access_token",
                        ["requested_token_type"] = "urn:ietf:params:oauth:token-type:access_token",
                        ["audience"] = settings.Audience,
                        ["scope"] = settings.Scope
                    })
                };

                using var response = await http.SendAsync(request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning(
                        "Token exchange for VStorage failed: {Status} {Body}",
                        (int)response.StatusCode, body.Length > MaxLoggedBody ? body[..MaxLoggedBody] : body);
                    return StorageErrors.Unavailable;
                }

                using var json = JsonDocument.Parse(body);
                var token = json.RootElement.GetProperty("access_token").GetString();
                var expiresIn = json.RootElement.TryGetProperty("expires_in", out var expires) && expires.TryGetInt32(out var seconds)
                    ? seconds
                    : 60;
                if (string.IsNullOrEmpty(token))
                {
                    return StorageErrors.Unavailable;
                }

                cache.Set(cacheKey, token, TimeSpan.FromSeconds(Math.Max(expiresIn - SafetySeconds, 5)));
                return token;
            }
            catch (Exception exception) when (
                exception is HttpRequestException or JsonException or KeyNotFoundException
                || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                logger.LogError(exception, "Token exchange for VStorage failed");
                return StorageErrors.Unavailable;
            }
        }
    }
}
