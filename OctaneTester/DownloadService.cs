using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using OctaneEngineCore;

namespace OctaneTester;

public class DownloadService(IEngine engine, IHostApplicationLifetime lifetime) : BackgroundService
{
    private const string Url = "https://plugins.jetbrains.com/files/7973/281233/sonarlint-intellij-7.4.0.60471.zip?updateId=281233&pluginId=7973&family=INTELLIJ";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pauseTokenSource = new PauseTokenSource();
        using var cancelTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        await engine.DownloadFile(new OctaneRequest(Url, null), pauseTokenSource, cancelTokenSource);
        lifetime.StopApplication();
    }
}