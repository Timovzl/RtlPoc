using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rtl.News.RtlPoc.Infrastructure.Databases.Migrations;

namespace Rtl.News.RtlPoc.Infrastructure.Databases;

public static class DatabaseInfrastructureRegistrationExtensions
{
    public static IServiceCollection AddDatabaseInfrastructureLayer(this IServiceCollection services, IConfiguration _)
    {
        services.AddMemoryCache();

        // Register the current project's dependencies
        services.Scan(scanner => scanner.FromAssemblies(typeof(DatabaseInfrastructureRegistrationExtensions).Assembly)
            .AddClasses(c => c.Where(type => !type.Name.Contains('<') && !type.IsNested), publicOnly: true) // Public only, for this project
            .AsSelfWithInterfaces().WithSingletonLifetime());

        return services;
    }

    /// <summary>
    /// Causes all relevant database migrations to be applied on host startup, in a concurrency-safe manner.
    /// </summary>
    public static IServiceCollection AddDatabaseMigrations(this IServiceCollection services)
    {
        services.AddSingleton<MigrationAssistant>();
        services.AddSingleton<IHostedService>(serviceProvider => serviceProvider.GetRequiredService<MigrationAssistant>());

        return services;
    }
}
