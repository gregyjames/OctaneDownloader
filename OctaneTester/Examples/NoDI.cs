using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using OctaneEngine;
using OctaneEngineCore;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace OctaneTester.Examples;

public class NoDI
{
    public void NoDIExample()
    {
        const string url = "https://plugins.jetbrains.com/files/7973/281233/sonarlint-intellij-7.4.0.60471.zip?updateId=281233&pluginId=7973&family=INTELLIJ";
        
        // Create engine using builder pattern - no DI required
        var engine = EngineBuilder.Create(config =>
        {
            config.Parts = 8;
            config.BufferSize = 8192;
            config.ShowProgress = true;
            config.NumRetries = 10;
            config.BytesPerSecond = 1;
            config.UseProxy = false;
            config.LowMemoryMode = false;
        }).Build();
        
        // Setup download
        var pauseTokenSource = new PauseTokenSource();
        using var cancelTokenSource = new CancellationTokenSource();
        
        // Download the file
        engine.DownloadFile(new OctaneRequest(url, null), pauseTokenSource, cancelTokenSource.Token).Wait();
    }

    public void NoDIWithLoggerExample()
    {
        const string url = "https://plugins.jetbrains.com/files/7973/281233/sonarlint-intellij-7.4.0.60471.zip?updateId=281233&pluginId=7973&family=INTELLIJ";
        
        // Setup logging
        var seriLog = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Information()
            .WriteTo.Async(a => a.Console(theme: AnsiConsoleTheme.Sixteen))
            .CreateLogger();
        var factory = LoggerFactory.Create(logging =>
        {
            logging.AddSerilog(seriLog);
        });
        
        // Create engine with logging - no DI required
        var engine = EngineBuilder.Create(config =>
        {
            config.Parts = 4;
            config.BufferSize = 16384;
            config.ShowProgress = false;
            config.NumRetries = 5;
            config.BytesPerSecond = 1;
            config.UseProxy = false;
            config.LowMemoryMode = true;
            config.RetryCap = 30;
        }, factory).Build();
        
        // Setup download
        var pauseTokenSource = new PauseTokenSource();
        using var cancelTokenSource = new CancellationTokenSource();
        
        // Download the file
        engine.DownloadFile(new OctaneRequest(url, null), pauseTokenSource, cancelTokenSource.Token).Wait();
    }

    public void NoDIMinimalExample()
    {
        const string url = "https://plugins.jetbrains.com/files/7973/281233/sonarlint-intellij-7.4.0.60471.zip?updateId=281233&pluginId=7973&family=INTELLIJ";
        
        // Create engine with minimal configuration - no DI required
        var engine = EngineBuilder.Create().Build();
        
        // Setup download
        var pauseTokenSource = new PauseTokenSource();
        using var cancelTokenSource = new CancellationTokenSource();
        
        // Download the file
        engine.DownloadFile(new OctaneRequest(url, null), pauseTokenSource, cancelTokenSource.Token).Wait();
    }

    public void NoDIDirectExample()
    {
        const string url = "https://plugins.jetbrains.com/files/7973/281233/sonarlint-intellij-7.4.0.60471.zip?updateId=281233&pluginId=7973&family=INTELLIJ";
        
        // Create configuration directly
        var config = new OctaneConfiguration
        {
            Parts = 6,
            BufferSize = 8192,
            ShowProgress = true,
            NumRetries = 3,
            BytesPerSecond = 1,
            UseProxy = false,
            LowMemoryMode = false
        };
        
        // Create engine directly without builder - no DI required
        var engine = EngineBuilder.Create()
            .WithConfiguration(config)
            .Build();
        
        // Setup download
        var pauseTokenSource = new PauseTokenSource();
        using var cancelTokenSource = new CancellationTokenSource();
        
        // Download the file
        engine.DownloadFile(new OctaneRequest(url, null), pauseTokenSource, cancelTokenSource.Token).Wait();
    }
} 