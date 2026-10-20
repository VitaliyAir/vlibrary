using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using Serilog;
using VLibrary.Api.Auth;
using VLibrary.Api.Endpoints;
using VLibrary.Api.Infrastructure;
using VLibrary.Application;
using VLibrary.Infrastructure;

const string ApiTitle = "VLibrary";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddApplication(builder.Configuration["MediatR:LicenseKey"]);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddKeycloakAuthentication(builder.Configuration);
builder.Services.AddKeycloakAuthorization(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "VLibrary API";
    document.Info.Version = "v1";
    document.Info.Description = "Personal virtual library: import, duplicates, catalog, shelves, notes, reminders.";
    return Task.CompletedTask;
}).AddDocumentTransformer<BearerSecuritySchemeTransformer>());
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyMethod()
    .AllowAnyHeader()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", true))
{
    await app.Services.ApplyMigrationsAsync();
}

// OpenAPI document (/openapi/v1.json) and Scalar UI (/scalar/v1) for trying the API.
if (app.Configuration.GetValue("OpenApi:Enabled", true))
{
    app.MapOpenApi().AllowAnonymous();
    // "Authorize" in Scalar signs in through Keycloak (public client, PKCE); the redirect comes back to /scalar/v1.
    var authOptions = AuthOptions.From(app.Configuration);
    app.MapScalarApiReference(options => options
            .WithTitle(ApiTitle)
            .AddPreferredSecuritySchemes([BearerSecuritySchemeTransformer.KeycloakScheme])
            .AddAuthorizationCodeFlow(BearerSecuritySchemeTransformer.KeycloakScheme, flow =>
            {
                flow.ClientId = authOptions.ClientId;
                flow.Pkce = Pkce.Sha256;
                flow.SelectedScopes = ["openid", "profile"];
            }))
        .AllowAnonymous();
}

app.MapGet("/version", () => Assembly.GetExecutingAssembly().GetName().Version?.ToString()).WithTags("System").AllowAnonymous();
app.MapGet("/name", () => ApiTitle).WithTags("System").AllowAnonymous();
app.MapGet("/env", () => app.Environment.EnvironmentName).WithTags("System").AllowAnonymous();
app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = HealthResponseWriter.WriteAsync }).AllowAnonymous();

var api = app.MapGroup("/api/v1");
api.MapImportEndpoints();
api.MapDuplicateEndpoints();
api.MapBookEndpoints();
api.MapCatalogEndpoints();
api.MapShelfEndpoints();
api.MapReadingEndpoints();
api.MapLibraryEndpoints();
api.MapCoverEndpoints();

await app.RunAsync();

/// <summary>Entry point; public for WebApplicationFactory in integration tests.</summary>
public partial class Program;
