using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Threading.Tasks;
using OctaneEngineCore.Interfaces.NetworkAnalyzer;

namespace OctaneEngineCore.Implementations.NetworkAnalyzer;

[ExcludeFromCodeCoverage]
public class HttpDownloader : IHttpDownloader
{
    public async Task<byte[]> GetByteArrayAsync(string url)
    {
        return await NetworkAnalyzer.SharedClient.GetByteArrayAsync(url).ConfigureAwait(false);
    }
}