using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using OctaneEngine;
using OctaneEngineCore;
using OctaneEngineCore.Clients;

namespace OctaneTester;

public class DownloadService(IEngine engine, IHostApplicationLifetime lifetime) : BackgroundService
{
    private const string Url = "https://plugins.jetbrains.com/files/7973/281233/sonarlint-intellij-7.4.0.60471.zip?updateId=281233&pluginId=7973&family=INTELLIJ";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pauseTokenSource = new PauseTokenSource();
        var task1 = engine.DownloadFile(new OctaneRequest(Url, "test1.zip"), pauseTokenSource, stoppingToken);
        var task2 = engine.DownloadFile(new OctaneRequest(Url, "test2.zip"), pauseTokenSource, stoppingToken);
        await Task.WhenAll(task1, task2);
        lifetime.StopApplication();
    }
}