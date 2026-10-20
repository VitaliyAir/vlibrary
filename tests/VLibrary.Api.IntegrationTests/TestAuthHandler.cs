using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VLibrary.Api.IntegrationTests
{
    /// <summary>Test stand-in for Keycloak: every request is signed in as a user holding the Owner role.</summary>
    internal sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
                [
                    new Claim("sub", "22222222-2222-2222-2222-222222222222"),
                    new Claim("preferred_username", "test-owner"),
                    new Claim(ClaimTypes.Role, "Owner")
                ],
                SchemeName,
                "preferred_username",
                ClaimTypes.Role);

            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
