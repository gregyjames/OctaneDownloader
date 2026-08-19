using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;

namespace OctaneEngineCore.Streams;

public class MemoryMappedFileWriter : IFileWriter
{
    private readonly MemoryMappedFile _mmf;

    public MemoryMappedFileWriter(MemoryMappedFile mmf)
    {
        _mmf = mmf ?? throw new ArgumentNullException(nameof(mmf));
    }

    public IChunkWriter CreateChunkWriter(long start, long length)
    {
        return new MemoryMappedChunkWriter(_mmf, start, length);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Dispose() { }

    private class MemoryMappedChunkWriter : IMemoryMappedChunkWriter
    {
        private readonly MemoryMappedFile _mmf;
        private readonly long _start;
        private readonly long _length;
        private Stream? _viewStream;

        public MemoryMappedChunkWriter(MemoryMappedFile mmf, long start, long length)
        {
            _mmf = mmf;
            _start = start;
            _length = length;
        }

        public async Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            _viewStream ??= _mmf.CreateViewStream(_start, _length);
            await _viewStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        public MemoryMappedViewAccessor CreateViewAccessor()
        {
            return _mmf.CreateViewAccessor(_start, _length);
        }

        public ValueTask DisposeAsync()
        {
            if (_viewStream != null)
                #if NETSTANDARD2_0
            return new ValueTask(_viewStream.DisposeAsync());
        #else
            return _viewStream.DisposeAsync();
#endif
            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
            _viewStream?.Dispose();
        }
    }
}
