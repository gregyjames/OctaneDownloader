using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace OctaneEngineCore;

public interface IEngine
{
    public Task DownloadFile(string url, string outFile = null, PauseTokenSource pauseTokenSource = null, CancellationTokenSource cancelTokenSource = null);
    public void SetProgressCallback(Action<double> callback);
    public void SetDoneCallback(Action<bool> callback);
    public void SetProxy(IWebProxy proxy);
}