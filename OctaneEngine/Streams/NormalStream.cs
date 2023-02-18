using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OctaneEngineCore.Streams;

internal class NormalStream: Stream, IStream
{
    private Stream _stream;
    public NormalStream(Stream stream)
    {
        _stream = stream;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new System.NotImplementedException();
    }

    public override void SetLength(long value)
    {
        throw new System.NotImplementedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return _stream.Read(buffer, offset, count);
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

    public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return await _stream.ReadAsync(buffer, offset, count, cancellationToken);
    }
}