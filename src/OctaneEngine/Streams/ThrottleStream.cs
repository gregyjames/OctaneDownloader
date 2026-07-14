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
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OctaneEngineCore.Streams;

public partial class ThrottleStream : Stream
{
    private readonly ILogger<ThrottleStream> _log;
    private int _maxBps;
    private Stream _parentStream;
    private TokenBucketRateLimiter? _rateLimiter;
    private readonly object _limiterLock = new();

    public ThrottleStream(ILoggerFactory factory)
    {
        _log = factory.CreateLogger<ThrottleStream>();
        // No throttling by default
    }
    
    public ThrottleStream(Stream parent, int maxBytesPerSecond, ILoggerFactory factory)
    {
        _parentStream = parent;
        _log = factory.CreateLogger<ThrottleStream>();
        SetBps(maxBytesPerSecond);
    }

    public void SetStreamParent(Stream stream)
    {
        _parentStream = stream;
    }

    public void SetBps(int maxBytesPerSecond)
    {
        _maxBps = maxBytesPerSecond;
        lock (_limiterLock)
        {
            var oldLimiter = _rateLimiter;
            
            if (maxBytesPerSecond > 0)
            {
                _rateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
                {
                    TokenLimit = maxBytesPerSecond,
                    TokensPerPeriod = Math.Max(1, maxBytesPerSecond / 10),
                    ReplenishmentPeriod = TimeSpan.FromMilliseconds(100),
                    AutoReplenishment = true,
                    QueueLimit = int.MaxValue,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                });
            }
            else
            {
                _rateLimiter = null;
            }

            oldLimiter?.Dispose();
        }
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
        LogThrottleStreamRead();
        TokenBucketRateLimiter? limiter;
        lock (_limiterLock) { limiter = _rateLimiter; }

        int maxToRead = count;
        if (limiter != null)
            maxToRead = Math.Min(count, _maxBps);

        var read = _parentStream.Read(buffer, offset, maxToRead);

        if (limiter != null && read > 0)
        {
            using var lease = limiter.AcquireAsync(read).AsTask().GetAwaiter().GetResult();
        }

        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        LogThrottleStreamRead();
        TokenBucketRateLimiter? limiter;
        lock (_limiterLock) { limiter = _rateLimiter; }

        int maxToRead = count;
        if (limiter != null)
            maxToRead = Math.Min(count, _maxBps);

        var read = await _parentStream.ReadAsync(buffer, offset, maxToRead, cancellationToken).ConfigureAwait(false);

        if (limiter != null && read > 0)
        {
            using var lease = await limiter.AcquireAsync(read, cancellationToken).ConfigureAwait(false);
            if (!lease.IsAcquired) cancellationToken.ThrowIfCancellationRequested();
        }

        return read;
    }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP || NET5_0_OR_GREATER
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
    {
        LogThrottleStreamRead();
        TokenBucketRateLimiter? limiter;
        lock (_limiterLock) { limiter = _rateLimiter; }

        int maxToRead = buffer.Length;
        if (limiter != null)
            maxToRead = Math.Min(maxToRead, _maxBps);

        var read = await _parentStream.ReadAsync(buffer[..maxToRead], token).ConfigureAwait(false);

        if (limiter != null && read > 0)
        {
            using var lease = await limiter.AcquireAsync(read, token).ConfigureAwait(false);
            if (!lease.IsAcquired) token.ThrowIfCancellationRequested();
        }

        return read;
    }
#endif

    public override void Write(byte[] buffer, int offset, int count)
    {
        LogThrottleStreamWrite();
        int totalWritten = 0;

        while (totalWritten < count)
        {
            int toWrite = count - totalWritten;
            TokenBucketRateLimiter? limiter;
            lock (_limiterLock) { limiter = _rateLimiter; }

            if (limiter != null)
                toWrite = Math.Min(toWrite, _maxBps);

            if (limiter != null)
            {
                using var lease = limiter.AcquireAsync(toWrite).AsTask().GetAwaiter().GetResult();
            }

            _parentStream.Write(buffer, offset + totalWritten, toWrite);
            totalWritten += toWrite;
        }
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        LogThrottleStreamWrite();
        int totalWritten = 0;

        while (totalWritten < count)
        {
            int toWrite = count - totalWritten;
            TokenBucketRateLimiter? limiter;
            lock (_limiterLock) { limiter = _rateLimiter; }

            if (limiter != null)
                toWrite = Math.Min(toWrite, _maxBps);

            if (limiter != null)
            {
                using var lease = await limiter.AcquireAsync(toWrite, cancellationToken).ConfigureAwait(false);
                if (!lease.IsAcquired) cancellationToken.ThrowIfCancellationRequested();
            }

            await _parentStream.WriteAsync(buffer, offset + totalWritten, toWrite, cancellationToken).ConfigureAwait(false);
            totalWritten += toWrite;
        }
    }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP || NET5_0_OR_GREATER
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
    {
        LogThrottleStreamWrite();
        int totalWritten = 0;
        int count = buffer.Length;

        while (totalWritten < count)
        {
            int toWrite = count - totalWritten;
            TokenBucketRateLimiter? limiter;
            lock (_limiterLock) { limiter = _rateLimiter; }

            if (limiter != null)
                toWrite = Math.Min(toWrite, _maxBps);

            if (limiter != null)
            {
                using var lease = await limiter.AcquireAsync(toWrite, token).ConfigureAwait(false);
                if (!lease.IsAcquired) token.ThrowIfCancellationRequested();
            }

            await _parentStream.WriteAsync(buffer.Slice(totalWritten, toWrite), token).ConfigureAwait(false);
            totalWritten += toWrite;
        }
    }
#endif

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _parentStream?.Dispose();
            lock (_limiterLock)
            {
                _rateLimiter?.Dispose();
            }
        }

        base.Dispose(disposing);
    }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP || NET5_0_OR_GREATER
    public override async ValueTask DisposeAsync()
    {
        if (_parentStream != null)
        {
            await _parentStream.DisposeAsync().ConfigureAwait(false);
        }
        lock (_limiterLock)
        {
            _rateLimiter?.Dispose();
        }
    }
#endif

    [LoggerMessage(LogLevel.Trace, "Throttle stream read")]
    partial void LogThrottleStreamRead();

    [LoggerMessage(LogLevel.Trace, "Throttle stream write")]
    partial void LogThrottleStreamWrite();
}