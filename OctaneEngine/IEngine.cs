using System.Threading;
using System.Threading.Tasks;

namespace OctaneEngineCore;

public interface IEngine
{
    public Task DownloadFile(string url, string outFile = null, PauseTokenSource pauseTokenSource = null, CancellationTokenSource cancelTokenSource = null);
}