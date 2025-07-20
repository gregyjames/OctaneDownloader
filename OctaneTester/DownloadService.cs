using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OctaneEngineCore;
using OctaneEngineCore.Clients;
using OctaneEngineCore.Interfaces;

namespace OctaneTester;

public class DownloadService(IEngine engine, IHostApplicationLifetime lifetime, ILogger<DownloadService> logger) : BackgroundService
{
    private const string Url = "https://plugins.jetbrains.com/files/7973/281233/sonarlint-intellij-7.4.0.60471.zip?updateId=281233&pluginId=7973&family=INTELLIJ";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pauseTokenSource = new PauseTokenSource();
        logger.LogInformation("Current Latency: {latency}", await engine.GetCurrentNetworkLatency());
        logger.LogInformation("Current Network speed: {speed}", await engine.GetCurrentNetworkSpeed());
        await engine.DownloadFile(new OctaneRequest(Url, "test1.zip"), pauseTokenSource, stoppingToken);
        lifetime.StopApplication();
    }
}