using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OctaneEngineCore.Streams;

internal class NormalStream: Stream, IStream
{
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stream?.Dispose();
        }

        base.Dispose(disposing);
    }

    private Stream _stream;
    
    public NormalStream()
    {
        _stream = null;
    }
    public NormalStream(Stream stream)
    {
        _stream = stream;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
       return _stream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        _stream.SetLength(value);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int bytesRead = 0;
        int totalBytesRead = 0;

        while (totalBytesRead < count)
        {
            bytesRead = _stream.Read(buffer, offset + totalBytesRead, count - totalBytesRead);
            if (bytesRead == 0) // End of stream
            {
                break;
            }
            totalBytesRead += bytesRead;
        }
        return totalBytesRead;
    }


    public override void Write(byte[] buffer, int offset, int count)
    {
        _stream.Write(buffer, offset, count);
    }

    public override bool CanRead => _stream.CanRead;
    public override bool CanSeek => _stream.CanSeek;

    public override bool CanWrite => _stream.CanWrite;

    public override long Length => _stream.Length;

    public override long Position
    {
        get => _stream.Position;
        set => _stream.Position = value;
    }

    public override void Flush()
    {
        _stream.Flush();
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        try
        {
            var memory = new Memory<byte>(buffer, offset, count);
            await _stream.ReadExactlyAsync(memory, cancellationToken);
            return count;
        }
        catch (EndOfStreamException)
        {
            // Handle end of stream if necessary
            return 0;
        }
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token)
    {
        try
        {
            return await _stream.ReadAsync(buffer, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Handle cancellation
            return 0;
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await _stream.DisposeAsync();
    }
    
    public void SetStreamParent(Stream stream)
    {
        _stream = stream;
    }

    public void SetBps(int maxBytesPerSecond)
    {
        // Method intentionally left empty.
    }
}
