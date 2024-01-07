using System;
using System.Threading;
using System.Threading.Tasks;

namespace OctaneEngineCore.Streams;

internal interface IStream: IDisposable
{
    public int Read(byte[] buffer, int offset, int count);
    public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);
    public Task<int> ReadAsync(Memory<byte> buffer, CancellationToken token);
    public ValueTask DisposeAsync();
}