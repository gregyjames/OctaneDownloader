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

using System;
using System.IO;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OctaneEngineCore.Streams;

public class ThrottleStream : Stream, IStream
{
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _parentStream?.Dispose();
        }

        base.Dispose(disposing);
    }

    ~ThrottleStream()
    {
        Dispose(false);
    }

    private readonly ILogger<ThrottleStream> _log;
    private int _maxBps;
    private Stream _parentStream;
    private readonly IScheduler _scheduler;
    private readonly IStopwatch _stopwatch;

    private long _processed;

    public ThrottleStream(ILoggerFactory factory)
    {
        _scheduler = Scheduler.Immediate;
        _log = factory.CreateLogger<ThrottleStream>();
        _stopwatch = _scheduler.StartStopwatch();
        _processed = 0;
    }
    
    public ThrottleStream(Stream parent, int maxBytesPerSecond, ILoggerFactory factory)
    {
        _maxBps = maxBytesPerSecond;
        _parentStream = parent;
        _scheduler = Scheduler.Immediate;
        _log = factory.CreateLogger<ThrottleStream>();
        _stopwatch = _scheduler.StartStopwatch();
        _processed = 0;
    }

    public override bool CanRead => _parentStream.CanRead;
    public override bool CanSeek => _parentStream.CanSeek;
    public override bool CanWrite => _parentStream.CanWrite;
    public override long Length => _parentStream.Length;

    public override long Position
    {
        get => _parentStream.Position;
        set => _parentStream.Position = value;
    }

    protected void Throttle(int bytes)
    {
        _processed += bytes;
       
        _log.LogTrace("Throttle stream processed {Processed} bytes", _processed);
        var targetTime = TimeSpan.FromSeconds((double)_processed / _maxBps);
        var actualTime = _stopwatch.Elapsed;
        var sleep = targetTime - actualTime;
        if (sleep > TimeSpan.Zero)
        {
            using var waitHandle = new AutoResetEvent(false);
            _scheduler.Sleep(sleep).GetAwaiter().OnCompleted(() => waitHandle.Set());
            waitHandle.WaitOne();
        }
    }

    public override void Flush()
    {
        _parentStream.Flush();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return _parentStream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        _parentStream.SetLength(value);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        _log.LogTrace("Throttle stream read");
        var read = _parentStream.Read(buffer, offset, count);
        Throttle(read);
        return read;
    }

    public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken token)
    {
        _log.LogTrace("Throttle stream read");
        var read = await _parentStream.ReadAsync(buffer, token);
        Throttle(read);
        return read;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _log.LogTrace("Throttle stream write");
        Throttle(count);
        _parentStream.Write(buffer, offset, count);
    }

    public override async ValueTask DisposeAsync()
    {
        await _parentStream.DisposeAsync();
    }
    
    public void SetStreamParent(Stream stream)
    {
        _parentStream = stream;
    }

    public void SetBps(int maxBytesPerSecond)
    {
        _maxBps = maxBytesPerSecond;
    }
}