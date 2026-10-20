using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using VLibrary.Application.Abstractions;

namespace VLibrary.Api.IntegrationTests
{
    /// <summary>
    /// Runs the API against a throw-away PostgreSQL 18 container (requires Docker).
    /// Migrations are applied on startup, exactly as in production. VStorage is replaced by <see cref="FakeBookStorage"/>.
    /// </summary>
    public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:18").Build();

        public async ValueTask InitializeAsync() => await _database.StartAsync();

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            await _database.DisposeAsync();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:VLibraryDb", _database.GetConnectionString());

            // The real JwtBearer setup only needs a syntactically valid config; requests are signed in by TestAuthHandler.
            builder.UseSetting("Authentication:Authority", "http://localhost/realms/test");
            builder.UseSetting("Authentication:ClientId", "vlibrary");
            builder.UseSetting("Authentication:RequireHttpsMetadata", "false");
            builder.UseSetting("Import:TempFolder", Path.Combine(Path.GetTempPath(), "vlibrary-tests", "import-temp"));

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IBookStorage>();
                services.AddSingleton<FakeBookStorage>();
                services.AddSingleton<IBookStorage>(sp => sp.GetRequiredService<FakeBookStorage>());

                services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            });
        }

        public FakeBookStorage Storage => Services.GetRequiredService<FakeBookStorage>();
    }

    [CollectionDefinition(Name)]
    public sealed class ApiCollection : ICollectionFixture<ApiFactory>
    {
        public const string Name = "api";
    }
}
