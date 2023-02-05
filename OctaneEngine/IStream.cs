using System.Threading;
using System.Threading.Tasks;

namespace OctaneEngineCore;

internal interface IStream
{
    public int Read(byte[] buffer, int offset, int count);
    public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);
}