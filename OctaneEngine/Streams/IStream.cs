using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OctaneEngineCore.Streams;

internal interface IStream
{
    public int Read(byte[] buffer, int offset, int count);
    public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);
    public void SetStreamParent(Stream stream);
    public void SetBPS(int maxBytesPerSecond);
}