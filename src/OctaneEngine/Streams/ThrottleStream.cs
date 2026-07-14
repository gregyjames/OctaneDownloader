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
    private sealed class LimiterSnapshot
    {
        public int Bps { get; }
        public TokenBucketRateLimiter? Limiter { get; }
        private int _refCount = 1;

        public LimiterSnapshot(int bps, TokenBucketRateLimiter? limiter)
        {
            Bps = bps;
            Limiter = limiter;
        }

        public void AddRef() => Interlocked.Increment(ref _refCount);

        public void Release()
        {
            if (Interlocked.Decrement(ref _refCount) == 0)
            {
                Limiter?.Dispose();
            }
        }
    }

    private readonly ILogger<ThrottleStream> _log;
    private Stream _parentStream;
    private LimiterSnapshot? _snapshot;
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
        LimiterSnapshot? oldSnapshot;
        lock (_limiterLock)
        {
            oldSnapshot = _snapshot;
            
            if (maxBytesPerSecond > 0)
            {
                int tokensPerPeriod = Math.Max(1, maxBytesPerSecond / 10);
                TimeSpan replenishmentPeriod = TimeSpan.FromSeconds((double)tokensPerPeriod / maxBytesPerSecond);

                _snapshot = new LimiterSnapshot(maxBytesPerSecond, new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
                {
                    TokenLimit = maxBytesPerSecond,
                    TokensPerPeriod = tokensPerPeriod,
                    ReplenishmentPeriod = replenishmentPeriod,
                    AutoReplenishment = true,
                    QueueLimit = int.MaxValue,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                }));
            }
            else
            {
                _snapshot = null;
            }
        }
        oldSnapshot?.Release();
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
        LimiterSnapshot? snap;
        lock (_limiterLock)
        {
            snap = _snapshot;
            snap?.AddRef();
        }

        try
        {
            int maxToRead = count;
            if (snap != null)
                maxToRead = Math.Min(count, snap.Bps);

            var read = _parentStream.Read(buffer, offset, maxToRead);

            if (snap?.Limiter != null && read > 0)
            {
                using var lease = snap.Limiter.AcquireAsync(read).AsTask().GetAwaiter().GetResult();
                if (!lease.IsAcquired) throw new InvalidOperationException("Rate limit lease was not acquired.");
            }

            return read;
        }
        finally
        {
            snap?.Release();
        }
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        LogThrottleStreamRead();
        LimiterSnapshot? snap;
        lock (_limiterLock)
        {
            snap = _snapshot;
            snap?.AddRef();
        }

        try
        {
            int maxToRead = count;
            if (snap != null)
                maxToRead = Math.Min(count, snap.Bps);

            var read = await _parentStream.ReadAsync(buffer, offset, maxToRead, cancellationToken).ConfigureAwait(false);

            if (snap?.Limiter != null && read > 0)
            {
                using var lease = await snap.Limiter.AcquireAsync(read, cancellationToken).ConfigureAwait(false);
                if (!lease.IsAcquired)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new InvalidOperationException("Rate limit lease was not acquired.");
                }
            }

            return read;
        }
        finally
        {
            snap?.Release();
        }
    }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP || NET5_0_OR_GREATER
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
    {
        LogThrottleStreamRead();
        LimiterSnapshot? snap;
        lock (_limiterLock)
        {
            snap = _snapshot;
            snap?.AddRef();
        }

        try
        {
            int maxToRead = buffer.Length;
            if (snap != null)
                maxToRead = Math.Min(maxToRead, snap.Bps);

            var read = await _parentStream.ReadAsync(buffer[..maxToRead], token).ConfigureAwait(false);

            if (snap?.Limiter != null && read > 0)
            {
                using var lease = await snap.Limiter.AcquireAsync(read, token).ConfigureAwait(false);
                if (!lease.IsAcquired)
                {
                    token.ThrowIfCancellationRequested();
                    throw new InvalidOperationException("Rate limit lease was not acquired.");
                }
            }

            return read;
        }
        finally
        {
            snap?.Release();
        }
    }
#endif

    public override void Write(byte[] buffer, int offset, int count)
    {
        LogThrottleStreamWrite();
        int totalWritten = 0;

        while (totalWritten < count)
        {
            LimiterSnapshot? snap;
            lock (_limiterLock)
            {
                snap = _snapshot;
                snap?.AddRef();
            }

            try
            {
                int toWrite = count - totalWritten;
                if (snap != null)
                    toWrite = Math.Min(toWrite, snap.Bps);

                if (snap?.Limiter != null)
                {
                    using var lease = snap.Limiter.AcquireAsync(toWrite).AsTask().GetAwaiter().GetResult();
                    if (!lease.IsAcquired) throw new InvalidOperationException("Rate limit lease was not acquired.");
                }

                _parentStream.Write(buffer, offset + totalWritten, toWrite);
                totalWritten += toWrite;
            }
            finally
            {
                snap?.Release();
            }
        }
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        LogThrottleStreamWrite();
        int totalWritten = 0;

        while (totalWritten < count)
        {
            LimiterSnapshot? snap;
            lock (_limiterLock)
            {
                snap = _snapshot;
                snap?.AddRef();
            }

            try
            {
                int toWrite = count - totalWritten;
                if (snap != null)
                    toWrite = Math.Min(toWrite, snap.Bps);

                if (snap?.Limiter != null)
                {
                    using var lease = await snap.Limiter.AcquireAsync(toWrite, cancellationToken).ConfigureAwait(false);
                    if (!lease.IsAcquired)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        throw new InvalidOperationException("Rate limit lease was not acquired.");
                    }
                }

                await _parentStream.WriteAsync(buffer, offset + totalWritten, toWrite, cancellationToken).ConfigureAwait(false);
                totalWritten += toWrite;
            }
            finally
            {
                snap?.Release();
            }
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
            LimiterSnapshot? snap;
            lock (_limiterLock)
            {
                snap = _snapshot;
                snap?.AddRef();
            }

            try
            {
                int toWrite = count - totalWritten;
                if (snap != null)
                    toWrite = Math.Min(toWrite, snap.Bps);

                if (snap?.Limiter != null)
                {
                    using var lease = await snap.Limiter.AcquireAsync(toWrite, token).ConfigureAwait(false);
                    if (!lease.IsAcquired)
                    {
                        token.ThrowIfCancellationRequested();
                        throw new InvalidOperationException("Rate limit lease was not acquired.");
                    }
                }

                await _parentStream.WriteAsync(buffer.Slice(totalWritten, toWrite), token).ConfigureAwait(false);
                totalWritten += toWrite;
            }
            finally
            {
                snap?.Release();
            }
        }
    }
#endif

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _parentStream?.Dispose();
            LimiterSnapshot? oldSnap;
            lock (_limiterLock)
            {
                oldSnap = _snapshot;
                _snapshot = null;
            }
            oldSnap?.Release();
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
        LimiterSnapshot? oldSnap;
        lock (_limiterLock)
        {
            oldSnap = _snapshot;
            _snapshot = null;
        }
        oldSnap?.Release();
    }
#endif

    [LoggerMessage(LogLevel.Trace, "Throttle stream read")]
    partial void LogThrottleStreamRead();

    [LoggerMessage(LogLevel.Trace, "Throttle stream write")]
    partial void LogThrottleStreamWrite();
}