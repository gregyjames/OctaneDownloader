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
using System.Runtime.CompilerServices;
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

        public bool TryAddRef()
        {
            while (true)
            {
                int current = Volatile.Read(ref _refCount);
                if (current <= 0)
                {
                    return false;
                }

                if (Interlocked.CompareExchange(ref _refCount, current + 1, current) == current)
                {
                    return true;
                }
            }
        }

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

                Volatile.Write(ref _snapshot, new LimiterSnapshot(maxBytesPerSecond, new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
                {
                    TokenLimit = maxBytesPerSecond,
                    TokensPerPeriod = tokensPerPeriod,
                    ReplenishmentPeriod = replenishmentPeriod,
                    AutoReplenishment = true,
                    QueueLimit = int.MaxValue,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                })));
            }
            else
            {
                Volatile.Write(ref _snapshot, null);
            }
        }
        oldSnapshot?.Release();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private LimiterSnapshot? AcquireSnapshot()
    {
        var snap = Volatile.Read(ref _snapshot);
        if (snap == null)
        {
            return null;
        }

        if (snap.TryAddRef())
        {
            return snap;
        }

        return AcquireSnapshotSlow();
    }

    private LimiterSnapshot? AcquireSnapshotSlow()
    {
        while (true)
        {
            var snap = Volatile.Read(ref _snapshot);
            if (snap == null)
            {
                return null;
            }

            if (snap.TryAddRef())
            {
                return snap;
            }
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

    private static async ValueTask AcquireLeaseAsync(TokenBucketRateLimiter limiter, int permitCount, CancellationToken cancellationToken)
    {
        using var lease = await limiter.AcquireAsync(permitCount, cancellationToken).ConfigureAwait(false);
        if (!lease.IsAcquired)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Rate limit lease was not acquired.");
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        LogThrottleStreamRead();
        var snap = AcquireSnapshot();
        if (snap == null)
        {
            return _parentStream.Read(buffer, offset, count);
        }

        try
        {
            int maxToRead = Math.Min(count, snap.Bps);
            var read = _parentStream.Read(buffer, offset, maxToRead);

            if (snap.Limiter != null && read > 0)
            {
                using var lease = snap.Limiter.AcquireAsync(read).AsTask().GetAwaiter().GetResult();
                if (!lease.IsAcquired) throw new InvalidOperationException("Rate limit lease was not acquired.");
            }

            return read;
        }
        finally
        {
            snap.Release();
        }
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        LogThrottleStreamRead();
        var snap = AcquireSnapshot();
        if (snap == null)
        {
            return _parentStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        return ReadAsyncThrottled(snap, buffer, offset, count, cancellationToken);
    }

    private async Task<int> ReadAsyncThrottled(LimiterSnapshot snap, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        try
        {
            int maxToRead = Math.Min(count, snap.Bps);
            var read = await _parentStream.ReadAsync(buffer, offset, maxToRead, cancellationToken).ConfigureAwait(false);

            if (snap.Limiter != null && read > 0)
            {
                await AcquireLeaseAsync(snap.Limiter, read, cancellationToken).ConfigureAwait(false);
            }

            return read;
        }
        finally
        {
            snap.Release();
        }
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
    {
        LogThrottleStreamRead();
        var snap = AcquireSnapshot();
        if (snap == null)
        {
            return _parentStream.ReadAsync(buffer, token);
        }

        return ReadAsyncThrottled(snap, buffer, token);
    }

    private async ValueTask<int> ReadAsyncThrottled(LimiterSnapshot snap, Memory<byte> buffer, CancellationToken token)
    {
        try
        {
            int maxToRead = Math.Min(buffer.Length, snap.Bps);
            var read = await _parentStream.ReadAsync(buffer[..maxToRead], token).ConfigureAwait(false);

            if (snap.Limiter != null && read > 0)
            {
                await AcquireLeaseAsync(snap.Limiter, read, token).ConfigureAwait(false);
            }

            return read;
        }
        finally
        {
            snap.Release();
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        LogThrottleStreamWrite();
        int totalWritten = 0;

        while (totalWritten < count)
        {
            var snap = AcquireSnapshot();
            if (snap == null)
            {
                _parentStream.Write(buffer, offset + totalWritten, count - totalWritten);
                return;
            }

            try
            {
                int toWrite = Math.Min(count - totalWritten, snap.Bps);

                if (snap.Limiter != null)
                {
                    using var lease = snap.Limiter.AcquireAsync(toWrite).AsTask().GetAwaiter().GetResult();
                    if (!lease.IsAcquired) throw new InvalidOperationException("Rate limit lease was not acquired.");
                }

                _parentStream.Write(buffer, offset + totalWritten, toWrite);
                totalWritten += toWrite;
            }
            finally
            {
                snap.Release();
            }
        }
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        LogThrottleStreamWrite();
        var snap = AcquireSnapshot();
        if (snap == null)
        {
            return _parentStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        return WriteAsyncThrottled(snap, buffer, offset, count, cancellationToken);
    }

    private async Task WriteAsyncThrottled(LimiterSnapshot firstSnap, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        int totalWritten = 0;
        LimiterSnapshot? snap = firstSnap;

        while (totalWritten < count)
        {
            snap ??= AcquireSnapshot();
            if (snap == null)
            {
                await _parentStream.WriteAsync(buffer, offset + totalWritten, count - totalWritten, cancellationToken).ConfigureAwait(false);
                return;
            }

            try
            {
                int toWrite = Math.Min(count - totalWritten, snap.Bps);

                if (snap.Limiter != null)
                {
                    await AcquireLeaseAsync(snap.Limiter, toWrite, cancellationToken).ConfigureAwait(false);
                }

                await _parentStream.WriteAsync(buffer, offset + totalWritten, toWrite, cancellationToken).ConfigureAwait(false);
                totalWritten += toWrite;
            }
            finally
            {
                snap.Release();
                snap = null;
            }
        }
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
    {
        LogThrottleStreamWrite();
        var snap = AcquireSnapshot();
        if (snap == null)
        {
            return _parentStream.WriteAsync(buffer, token);
        }

        return WriteAsyncThrottled(snap, buffer, token);
    }

    private async ValueTask WriteAsyncThrottled(LimiterSnapshot firstSnap, ReadOnlyMemory<byte> buffer, CancellationToken token)
    {
        int totalWritten = 0;
        int count = buffer.Length;
        LimiterSnapshot? snap = firstSnap;

        while (totalWritten < count)
        {
            snap ??= AcquireSnapshot();
            if (snap == null)
            {
                await _parentStream.WriteAsync(buffer.Slice(totalWritten, count - totalWritten), token).ConfigureAwait(false);
                return;
            }

            try
            {
                int toWrite = Math.Min(count - totalWritten, snap.Bps);

                if (snap.Limiter != null)
                {
                    await AcquireLeaseAsync(snap.Limiter, toWrite, token).ConfigureAwait(false);
                }

                await _parentStream.WriteAsync(buffer.Slice(totalWritten, toWrite), token).ConfigureAwait(false);
                totalWritten += toWrite;
            }
            finally
            {
                snap.Release();
                snap = null;
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _parentStream?.Dispose();
            LimiterSnapshot? oldSnap;
            lock (_limiterLock)
            {
                oldSnap = _snapshot;
                Volatile.Write(ref _snapshot, null);
            }
            oldSnap?.Release();
        }

        base.Dispose(disposing);
    }

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
            Volatile.Write(ref _snapshot, null);
        }
        oldSnap?.Release();
        GC.SuppressFinalize(this);
    }

    [LoggerMessage(LogLevel.Trace, "Throttle stream read")]
    partial void LogThrottleStreamRead();

    [LoggerMessage(LogLevel.Trace, "Throttle stream write")]
    partial void LogThrottleStreamWrite();
}