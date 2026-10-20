using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using VLibrary.Application.Abstractions;
using VLibrary.Infrastructure.Auth;

namespace VLibrary.Api.Auth
{
    public static class AuthenticationConfiguration
    {
        public static IServiceCollection AddKeycloakAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            var options = AuthOptions.From(configuration);

            // Who did it (audit log): the user name from the access token of the current request.
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUser, HttpCurrentUser>();

            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(jwt =>
                {
                    jwt.Authority = options.Authority;
                    jwt.RequireHttpsMetadata = options.RequireHttpsMetadata;
                    jwt.MapInboundClaims = false;

                    // Docker: discovery and signing keys are fetched through the internal Keycloak address.
                    if (KeycloakBackchannelHandler.Create(options.Authority, options.InternalAuthority) is { } backchannel)
                    {
                        jwt.BackchannelHttpHandler = backchannel;
                    }

                    jwt.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidAudience = options.ClientId,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ClockSkew = TimeSpan.FromSeconds(options.ClockSkewSeconds),
                        NameClaimType = "preferred_username",
                        RoleClaimType = ClaimTypes.Role
                    };

                    jwt.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = context =>
                        {
                            if (context.Principal?.Identity is ClaimsIdentity identity)
                            {
                                MapClientRoles(identity, options.ClientId);
                            }

                            return Task.CompletedTask;
                        }
                    };
                });

            return services;
        }

        /// <summary>
        /// Secure by default: any endpoint without explicit authorization metadata requires a signed-in user with
        /// the client role (Owner). Public endpoints need an explicit AllowAnonymous.
        /// </summary>
        public static IServiceCollection AddKeycloakAuthorization(this IServiceCollection services, IConfiguration configuration)
        {
            var options = AuthOptions.From(configuration);

            services.AddAuthorizationBuilder()
                .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .RequireRole(options.RequiredRole)
                    .Build());

            return services;
        }

        // Client roles live in the nested claim resource_access.<clientId>.roles, which ASP.NET does not read itself.
        // Only the roles of this app's own client are taken: another app's role does not grant access here.
        private static void MapClientRoles(ClaimsIdentity identity, string clientId)
        {
            var raw = identity.FindFirst("resource_access")?.Value;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(clientId, out var client)
                || !client.TryGetProperty("roles", out var roles)
                || roles.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var role in roles.EnumerateArray())
            {
                var value = role.GetString();
                if (!string.IsNullOrWhiteSpace(value) && !identity.HasClaim(ClaimTypes.Role, value))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, value));
                }
            }
        }
    }
}
