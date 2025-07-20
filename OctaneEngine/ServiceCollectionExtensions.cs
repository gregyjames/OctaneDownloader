using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OctaneEngine.Clients;
using OctaneEngine.ShellProgressBar;

namespace OctaneEngine;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Octane Engine services to the service collection for use with IHostBuilder
    /// </summary>
    public static IHostBuilder UseOctaneEngine(this IHostBuilder hostBuilder)
    {
        hostBuilder.ConfigureServices((context, services) =>
        {
            services.Configure<OctaneConfiguration>(context.Configuration.GetSection("Octane"));
            services.AddProgressBar();
            services.AddClient();
            services.AddTransient<IEngine, Engine>();
        });
        
        return hostBuilder;
    }
    
    /// <summary>
    /// Adds Octane Engine services to the service collection for use with IHostBuilder
    /// </summary>
    public static IHostBuilder UseOctaneEngine(this IHostBuilder hostBuilder, Action<OctaneConfiguration> configure)
    {
        hostBuilder.ConfigureServices((context, services) =>
        {
            services.Configure<OctaneConfiguration>(configure);
            services.AddProgressBar();
            services.AddClient();
            services.AddTransient<IEngine, Engine>();
        });
        
        return hostBuilder;
    }

    /// <summary>
    /// Adds Octane Engine services to the service collection for use without IHostBuilder
    /// </summary>
    public static IServiceCollection AddOctaneEngine(this IServiceCollection services)
    {
        services.AddProgressBar();
        services.AddClient();
        services.AddTransient<IEngine, Engine>();
        return services;
    }

    /// <summary>
    /// Adds Octane Engine services to the service collection for use without IHostBuilder
    /// </summary>
    public static IServiceCollection AddOctaneEngine(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OctaneConfiguration>(configuration.GetSection("Octane"));
        services.AddProgressBar();
        services.AddClient();
        services.AddTransient<IEngine, Engine>();
        return services;
    }

    /// <summary>
    /// Adds Octane Engine services to the service collection for use without IHostBuilder
    /// </summary>
    public static IServiceCollection AddOctaneEngine(this IServiceCollection services, Action<OctaneConfiguration> configure)
    {
        services.Configure<OctaneConfiguration>(configure);
        services.AddProgressBar();
        services.AddClient();
        services.AddTransient<IEngine, Engine>();
        return services;
    }

    /// <summary>
    /// Adds Octane Engine services to the service collection for use without IHostBuilder
    /// </summary>
    public static IServiceCollection AddOctaneEngine(this IServiceCollection services, OctaneConfiguration configuration)
    {
        services.AddSingleton<IOptions<OctaneConfiguration>>(Options.Create(configuration));
        services.AddProgressBar();
        services.AddClient();
        services.AddTransient<IEngine, Engine>();
        return services;
    }
}