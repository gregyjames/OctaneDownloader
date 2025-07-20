using System;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OctaneEngine;
using OctaneEngineCore.ShellProgressBar;

namespace OctaneEngineCore.Clients;

public enum ClientTypes { Octane, Normal }
public static class ClientModule
{
    public static void AddClient(this IServiceCollection services)
    {
        services.AddSingleton<OctaneHTTPClientPool>(provider =>
        {
            var config = provider.GetRequiredService<IOptions<OctaneConfiguration>>().Value;
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            return new OctaneHTTPClientPool(config, loggerFactory);
        });
    }
}