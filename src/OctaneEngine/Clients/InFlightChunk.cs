using System;
using System.Diagnostics;
using System.Threading;

namespace OctaneEngineCore.Clients;

internal sealed class InFlightChunk : IDisposable
{
    public (long start, long end) Piece { get; }
    public long TotalBytes => Piece.end - Piece.start + 1;
    public long StartTimestamp { get; }
    public CancellationTokenSource Cts { get; }

    private long _bytesCommitted;
    private long _lastProgressTimestamp;
    private int _completed;
    private int _hedged;

    public InFlightChunk((long start, long end) piece, CancellationToken parentToken)
    {
        Piece = piece;
        long now = Stopwatch.GetTimestamp();
        StartTimestamp = now;
        _lastProgressTimestamp = now;
        Cts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
    }

    public long BytesCommitted => Volatile.Read(ref _bytesCommitted);
    public long LastProgressTimestamp => Volatile.Read(ref _lastProgressTimestamp);
    public bool IsCompleted => Volatile.Read(ref _completed) != 0;
    public bool IsHedged => Volatile.Read(ref _hedged) != 0;

    /// <summary>
    /// Records the number of contiguous bytes from <see cref="Piece"/>.start that have been
    /// durably written to the file handle. Must only be called after the corresponding write completes.
    /// </summary>
    public void ReportBytesCommitted(long contiguousBytesCommitted)
    {
        long current = Volatile.Read(ref _bytesCommitted);
        if (contiguousBytesCommitted > current)
        {
            Volatile.Write(ref _bytesCommitted, contiguousBytesCommitted);
            Volatile.Write(ref _lastProgressTimestamp, Stopwatch.GetTimestamp());
        }
    }

    public bool TryClaimHedge() => !IsCompleted && Interlocked.CompareExchange(ref _hedged, 1, 0) == 0;

    public bool TryMarkCompleted()
    {
        if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
        {
            return false;
        }

        try
        {
            Cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        return true;
    }

    public void Dispose() => Cts.Dispose();
}
