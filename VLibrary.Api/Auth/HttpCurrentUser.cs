using System.Security.Claims;
using Microsoft.Net.Http.Headers;
using VLibrary.Application.Abstractions;

namespace VLibrary.Api.Auth
{
    /// <summary>
    /// User of the current HTTP request: claims of the validated Keycloak access token
    /// (MapInboundClaims is off, so the JWT claim names are kept as is).
    /// </summary>
    internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
    {
        private const string BearerPrefix = "Bearer ";

        public string? UserName => User?.FindFirst("preferred_username")?.Value ?? UserId;

        public string? UserId => User?.FindFirst("sub")?.Value;

        public string? AccessToken
        {
            get
            {
                if (User == null)
                {
                    return null;
                }

                var header = httpContextAccessor.HttpContext!.Request.Headers[HeaderNames.Authorization].ToString();
                return header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)
                    ? header[BearerPrefix.Length..].Trim()
                    : null;
            }
        }

        private ClaimsPrincipal? User =>
            httpContextAccessor.HttpContext?.User is { Identity.IsAuthenticated: true } user ? user : null;
    }
}
