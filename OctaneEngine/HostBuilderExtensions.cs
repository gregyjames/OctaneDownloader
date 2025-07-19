using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OctaneEngine;
using OctaneEngineCore.Clients;
using OctaneEngineCore.ShellProgressBar;
using Microsoft.Extensions.Options;

namespace OctaneEngineCore;

public static class HostBuilderExtensions
{
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
}