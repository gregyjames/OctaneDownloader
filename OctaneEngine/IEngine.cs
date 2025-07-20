using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using OctaneEngine.Clients;

namespace OctaneEngine;

public interface IEngine
{
    public Task DownloadFile(OctaneRequest request, PauseTokenSource pauseTokenSource = null, CancellationToken token = default);
    public void SetProgressCallback(Action<double> callback);
    public void SetDoneCallback(Action<bool> callback);
    public void SetProxy(IWebProxy proxy);
}