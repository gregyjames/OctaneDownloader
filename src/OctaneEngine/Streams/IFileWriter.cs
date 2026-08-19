using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;

namespace OctaneEngineCore.Streams;

public interface IChunkWriter : IAsyncDisposable, IDisposable
{
    Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken);
}

public interface IMemoryMappedChunkWriter : IChunkWriter
{
    MemoryMappedViewAccessor CreateViewAccessor();
}

public interface IFileWriter : IAsyncDisposable, IDisposable
{
    IChunkWriter CreateChunkWriter(long start, long length);
}
