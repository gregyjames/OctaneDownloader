using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OctaneEngineCore.Streams;

public class BufferStream : Stream
{
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _innerStream.Dispose();
        }

        base.Dispose(disposing);
    }

    private readonly Stream _innerStream;
    private readonly byte[] _buffer;
    private int _position;
    private int _availableBytes;
    private bool _flushed;

    public BufferStream(Stream innerStream, byte[] buffer)
    {
        _innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _position = 0;
        _availableBytes = 0;
        _flushed = true;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => _innerStream.Length;

    public override long Position
    {
        get => _innerStream.Position;
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
        if (!_flushed && _availableBytes > 0)
        {
            _innerStream.Write(_buffer, 0, _availableBytes);
            _availableBytes = 0;
        }
        _innerStream.Flush();
        _flushed = true;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_availableBytes == 0)
        {
            _position = 0;
            _availableBytes = _innerStream.Read(_buffer, 0, _buffer.Length);
            _flushed = false;
        }

        int bytesRead = Math.Min(count, _availableBytes);
        Buffer.BlockCopy(_buffer, _position, buffer, offset, bytesRead);
        _position += bytesRead;
        _availableBytes -= bytesRead;
        return bytesRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_availableBytes == 0)
        {
            _position = 0;
            _availableBytes = await _innerStream.ReadAsync(_buffer, 0, _buffer.Length, cancellationToken);
            _flushed = false;
        }

        int bytesRead = Math.Min(count, _availableBytes);
        Buffer.BlockCopy(_buffer, _position, buffer, offset, bytesRead);
        _position += bytesRead;
        _availableBytes -= bytesRead;
        return bytesRead;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (count >= _buffer.Length)
        {
            Flush();
            _innerStream.Write(buffer, offset, count);
        }
        else
        {
            if (count > _buffer.Length - _availableBytes)
            {
                Flush();
            }
            Buffer.BlockCopy(buffer, offset, _buffer, _availableBytes, count);
            _availableBytes += count;
        }
        _flushed = false;
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (count >= _buffer.Length)
        {
            Flush();
            await _innerStream.WriteAsync(buffer, offset, count, cancellationToken);
        }
        else
        {
            if (count > _buffer.Length - _availableBytes)
            {
                Flush();
            }
            Buffer.BlockCopy(buffer, offset, _buffer, _availableBytes, count);
            _availableBytes += count;
        }
        _flushed = false;

        if (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException();
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }
}