/*
 * The MIT License (MIT)
 * Copyright (c) 2015 Greg James
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NON-INFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OctaneEngineCore;

public class PauseTokenSource
{
    private volatile TaskCompletionSource<bool> _paused;
    private ILoggerFactory _factory;
    private ILogger _log;
    internal static readonly Task _completedTask = Task.FromResult(true);

    public PauseTokenSource(ILoggerFactory factory)
    {
        _factory = factory;
        _log = _factory.CreateLogger<PauseTokenSource>();
    }
    public PauseToken Token => new PauseToken(this, _log);
    public bool IsPaused => _paused != null;

    public void Pause()
    {
        _log.LogInformation("Download task was paused...");
        Interlocked.CompareExchange(ref _paused, new TaskCompletionSource<bool>(), null);
    }

    public void Resume()
    {
        _log.LogInformation("Download task was resumed!");
        while (true)
        {
            var tcs = _paused;

            if (tcs == null)
                return;

            if (Interlocked.CompareExchange(ref _paused, null, tcs) != tcs) continue;
            tcs.SetResult(true);
            break;
        }
    }

    public Task WaitWhilePausedAsync()
    {
        _log.LogInformation("Waiting for download task to resume...");
        return _paused?.Task ?? _completedTask;
    }
}