using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VLibrary.Application.Abstractions;
using VLibrary.Infrastructure.Auth;

namespace VLibrary.Infrastructure.Storage
{
    internal static class VStorageRegistration
    {
        /// <summary>
        /// Book files in VStorage. When the "VStorage" section is incomplete (BaseUrl, client secret) the API still starts;
        /// imports, opening books and the library check then answer "VStorage is not reachable or not configured".
        /// </summary>
        public static IServiceCollection AddVStorage(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddMemoryCache();
            services.Configure<VStorageOptions>(configuration.GetSection(VStorageOptions.SectionName));
            services.PostConfigure<VStorageOptions>(options =>
            {
                var authority = configuration["Authentication:Authority"];
                if (string.IsNullOrWhiteSpace(options.TokenEndpoint) && !string.IsNullOrWhiteSpace(authority))
                {
                    options.TokenEndpoint = $"{authority.TrimEnd('/')}/protocol/openid-connect/token";
                }
            });

            // Docker: the token exchange goes through the internal Keycloak address (Authentication:InternalAuthority).
            services.AddHttpClient<VStorageTokenProvider>(client => client.Timeout = TimeSpan.FromSeconds(30))
                .ConfigurePrimaryHttpMessageHandler(() => KeycloakBackchannelHandler.Create(
                    configuration["Authentication:Authority"], configuration["Authentication:InternalAuthority"]) ?? new SocketsHttpHandler());
            services.AddHttpClient<IBookStorage, VStorageBookStorage>((serviceProvider, client) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<VStorageOptions>>().Value;
                if (Uri.TryCreate(settings.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseAddress))
                {
                    client.BaseAddress = baseAddress;
                }

                client.Timeout = settings.Timeout;
            });

            return services;
        }
    }
}
