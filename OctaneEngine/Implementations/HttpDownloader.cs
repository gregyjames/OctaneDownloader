using System.Net.Http;
using System.Threading.Tasks;

namespace OctaneEngineCore.Implementations;

public class HttpDownloader : IHttpDownloader
{
    public async Task<byte[]> GetByteArrayAsync(string url)
    {
        using var client = new HttpClient();
        return await client.GetByteArrayAsync(url);
    }
}