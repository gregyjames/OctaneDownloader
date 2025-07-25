using System.IO;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OctaneEngineCore;
using OctaneEngineCore.Clients;
using OctaneEngineCore.Interfaces;

namespace OctaneTester.Examples;

public class NoLogger
{
    public void NoLoggerExample()
    {
        const string url = "https://plugins.jetbrains.com/files/7973/281233/sonarlint-intellij-7.4.0.60471.zip?updateId=281233&pluginId=7973&family=INTELLIJ";
        
        // Setup configuration
        var builder = new ConfigurationBuilder();
        builder.SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true);
        var configRoot = builder.Build();
        
        // Setup dependency injection without IHostBuilder
        var services = new ServiceCollection();
        
        // Add Octane Engine with configuration from JSON (no logging setup)
        services.AddOctaneEngine(configRoot);
        
        // Build the service provider
        var serviceProvider = services.BuildServiceProvider();
        
        // Get the engine from DI
        var engine = serviceProvider.GetRequiredService<IEngine>();
        
        // Setup download
        var pauseTokenSource = new PauseTokenSource();
        using var cancelTokenSource = new CancellationTokenSource();
        
        // Download the file
        engine.DownloadFile(new OctaneRequest(url, null), pauseTokenSource, cancelTokenSource.Token).Wait();
        
        // Cleanup
        serviceProvider.Dispose();
    }
}