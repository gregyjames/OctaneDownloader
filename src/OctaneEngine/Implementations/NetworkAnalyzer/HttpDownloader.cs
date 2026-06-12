using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Threading.Tasks;
using OctaneEngineCore.Interfaces.NetworkAnalyzer;

namespace OctaneEngineCore.Implementations.NetworkAnalyzer;

[ExcludeFromCodeCoverage]
public class HttpDownloader : IHttpDownloader
{
    private static readonly HttpClient SharedClient = new HttpClient();
    public async Task<byte[]> GetByteArrayAsync(string url)
    {
        return await SharedClient.GetByteArrayAsync(url);
    }
}
