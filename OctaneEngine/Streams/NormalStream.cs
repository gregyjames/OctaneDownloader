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

    public Stream _stream { get; set; }

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
        var bytesRead = 0;
        var totalBytesRead = 0;

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
        var bytesRead = 0;
        var totalBytesRead = 0;
        while (totalBytesRead < count)
        {
            bytesRead = await _stream.ReadAsync(buffer, offset + totalBytesRead, count - totalBytesRead, cancellationToken);
            if (bytesRead == 0) // End of stream
            {
                break;
            }
            totalBytesRead += bytesRead;
        }
        return totalBytesRead;
    }

    public void SetStreamParent(Stream stream)
    {
        _stream = stream;
    }

    public void SetBPS(int maxBytesPerSecond)
    {
        // Method intentionally left empty.
    }
}
