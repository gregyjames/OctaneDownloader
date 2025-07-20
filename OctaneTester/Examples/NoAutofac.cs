using System.IO;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OctaneEngineCore;
using OctaneEngineCore.Clients;
using OctaneEngineCore.Interfaces;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace OctaneTester.Examples;

public class NoAutofac
{
    public void NoAutoFacExample()
    {
        const string url = "https://plugins.jetbrains.com/files/7973/281233/sonarlint-intellij-7.4.0.60471.zip?updateId=281233&pluginId=7973&family=INTELLIJ";
        
        // Setup logging
        var seriLog = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Error()
            .WriteTo.Async(a => a.Console(theme: AnsiConsoleTheme.Sixteen))
            .CreateLogger();
        var factory = LoggerFactory.Create(logging =>
        {
            logging.AddSerilog(seriLog);
        });

        // Setup configuration
        var builder = new ConfigurationBuilder();
        builder.SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true);
        var configRoot = builder.Build();

        // Setup dependency injection without IHostBuilder
        var services = new ServiceCollection();
        
        // Add logging
        services.AddSingleton<ILoggerFactory>(factory);
        
        // Add Octane Engine with configuration from JSON
        services.AddOctaneEngine(configRoot);
        
        // Build the service provider
        var serviceProvider = services.BuildServiceProvider();
        
        // Get the engine from DI
        var engine = serviceProvider.GetRequiredService<IEngine>();
        
        // Setup download
        var pauseTokenSource = new PauseTokenSource();
        using var cancelTokenSource = new CancellationTokenSource();
        
        // Download the file
        engine.DownloadFile(new OctaneRequest(url, null), pauseTokenSource).Wait();
        
        // Cleanup
        serviceProvider.Dispose();
    }
}