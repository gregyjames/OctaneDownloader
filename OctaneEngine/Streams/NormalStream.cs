using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OctaneEngineCore.Streams;

internal class NormalStream: IStream
{
    private Stream _stream;
    public NormalStream(Stream stream)
    {
        _stream = stream;
    }
    public int Read(byte[] buffer, int offset, int count)
    {
        return _stream.Read(buffer, offset, count);
    }

    public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return await _stream.ReadAsync(buffer, offset, count, cancellationToken);
    }
}