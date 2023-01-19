using System.Threading;
using System.Threading.Tasks;

namespace OctaneEngineCore;

internal class PauseTokenSource
{
    private volatile TaskCompletionSource<bool> _paused;
    internal static readonly Task _completedTask = Task.FromResult(true);

    public PauseToken Token => new PauseToken(this);
    public bool IsPaused => _paused != null;

    public void Pause()
    {
        // if (tcsPause == new TaskCompletionSource<bool>()) tcsPause = null;
        Interlocked.CompareExchange(ref _paused, new TaskCompletionSource<bool>(), null);
    }

    public void Resume()
    {
        while (true)
        {
            var tcs = _paused;

            if (tcs == null)
                return;

            if (Interlocked.CompareExchange(ref _paused, null, tcs) == tcs)
            {
                tcs.SetResult(true);
                break;
            }
        }
    }

    internal Task WaitWhilePausedAsync()
    {
        return _paused?.Task ?? _completedTask;
    }
}