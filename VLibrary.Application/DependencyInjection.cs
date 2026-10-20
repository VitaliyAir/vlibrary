using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using VLibrary.Application.Behaviors;
using VLibrary.Application.Features.Books;
using VLibrary.Application.Features.Categories;
using VLibrary.Application.Features.Covers;
using VLibrary.Application.Features.Imports;

namespace VLibrary.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services, string? mediatRLicenseKey)
        {
            var assembly = typeof(DependencyInjection).Assembly;

            services.AddMediatR(cfg =>
            {
                cfg.LicenseKey = mediatRLicenseKey;
                cfg.RegisterServicesFromAssembly(assembly);
                cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
                cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            });

            services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

            services.AddScoped<CatalogResolvers>();
            services.AddScoped<BookCreator>();
            services.AddScoped<CategoryResolver>();
            services.AddScoped<ImportFileProcessor>();
            services.AddScoped<CoverAttacher>();

            return services;
        }
    }
}
