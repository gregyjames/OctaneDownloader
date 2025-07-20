using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Threading.Tasks;

namespace OctaneEngineCore.Implementations;

[ExcludeFromCodeCoverage]
public class HttpDownloader : IHttpDownloader
{
    public async Task<byte[]> GetByteArrayAsync(string url)
    {
        using var client = new HttpClient();
        return await client.GetByteArrayAsync(url);
    }
}