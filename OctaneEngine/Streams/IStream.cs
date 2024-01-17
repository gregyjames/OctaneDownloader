using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OctaneEngineCore.Streams;

internal interface IStream: IDisposable
{
    public int Read(byte[] buffer, int offset, int count);
    public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);
    public Task<int> ReadAsync(Memory<byte> buffer, CancellationToken token);
    public void SetStreamParent(Stream stream);
    public void SetBps(int maxBytesPerSecond);
    public ValueTask DisposeAsync();
}