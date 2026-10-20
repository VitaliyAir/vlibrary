namespace VLibrary.Api.Auth
{
    public sealed class AuthOptions
    {
        public const string SectionName = "Authentication";

        /// <summary>Keycloak realm address, e.g. http://localhost:8083/realms/VEco (the token issuer).</summary>
        public string Authority { get; set; } = string.Empty;

        /// <summary>Keycloak client id of this app: it is the required token audience and the source of roles.</summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>Client role that grants access to the API.</summary>
        public string RequiredRole { get; set; } = AppRoles.Owner;

        /// <summary>Switched off only for a local http Keycloak in Development.</summary>
        public bool RequireHttpsMetadata { get; set; } = true;

        /// <summary>
        /// Docker only: how this API reaches Keycloak when that differs from <see cref="Authority"/> as the browser sees it,
        /// e.g. http://host.docker.internal:8083. Empty = use Authority directly (dev, tests).
        /// </summary>
        public string? InternalAuthority { get; set; }

        public int ClockSkewSeconds { get; set; } = 30;

        public static AuthOptions From(IConfiguration configuration)
        {
            var options = configuration.GetSection(SectionName).Get<AuthOptions>() ?? new AuthOptions();

            if (string.IsNullOrWhiteSpace(options.Authority) || string.IsNullOrWhiteSpace(options.ClientId))
            {
                throw new InvalidOperationException(
                    $"{SectionName}:Authority and {SectionName}:ClientId must be configured.");
            }

            return options;
        }
    }
}
