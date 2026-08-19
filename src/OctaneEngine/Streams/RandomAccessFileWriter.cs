#if NET6_0_OR_GREATER
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace OctaneEngineCore.Streams;

public class RandomAccessFileWriter : IFileWriter
{
    private readonly SafeFileHandle _fileHandle;

    public RandomAccessFileWriter(SafeFileHandle fileHandle)
    {
        _fileHandle = fileHandle ?? throw new ArgumentNullException(nameof(fileHandle));
    }

    public IChunkWriter CreateChunkWriter(long start, long length)
    {
        return new RandomAccessChunkWriter(_fileHandle, start);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
    }

    private class RandomAccessChunkWriter : IChunkWriter
    {
        private readonly SafeFileHandle _fileHandle;
        private long _currentOffset;

        public RandomAccessChunkWriter(SafeFileHandle fileHandle, long startOffset)
        {
            _fileHandle = fileHandle;
            _currentOffset = startOffset;
        }

        public async Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            await RandomAccess.WriteAsync(_fileHandle, buffer, _currentOffset, cancellationToken).ConfigureAwait(false);
            _currentOffset += buffer.Length;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void Dispose() { }
    }
}
#endif
