using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace VLibrary.Api.Auth
{
    /// <summary>
    /// Adds two alternative security schemes to the OpenAPI document, so Scalar can call the protected API:
    /// "Keycloak" — sign in through Keycloak (authorization code + PKCE, Scalar's "Authorize"),
    /// "Bearer" — paste an access token by hand.
    /// </summary>
    internal sealed class BearerSecuritySchemeTransformer(IConfiguration configuration) : IOpenApiDocumentTransformer
    {
        public const string KeycloakScheme = "Keycloak";
        public const string BearerScheme = "Bearer";

        public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            var options = AuthOptions.From(configuration);
            var openIdConnect = options.Authority.TrimEnd('/') + "/protocol/openid-connect";

            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

            document.Components.SecuritySchemes[KeycloakScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Description = $"Sign in with Keycloak (client {options.ClientId}, role {options.RequiredRole}).",
                Flows = new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri(openIdConnect + "/auth"),
                        TokenUrl = new Uri(openIdConnect + "/token"),
                        Scopes = new Dictionary<string, string>
                        {
                            ["openid"] = "OpenID Connect sign-in",
                            ["profile"] = "User name"
                        }
                    }
                }
            };

            document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = $"Keycloak access token (client {options.ClientId})."
            };

            // Separate requirements = either scheme is enough.
            document.Security ??= [];
            document.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(KeycloakScheme, document)] = ["openid", "profile"]
            });
            document.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerScheme, document)] = []
            });

            return Task.CompletedTask;
        }
    }
}
