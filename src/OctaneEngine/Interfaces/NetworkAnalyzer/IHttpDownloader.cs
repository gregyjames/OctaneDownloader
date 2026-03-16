
using System.Threading.Tasks;

namespace OctaneEngineCore.Interfaces.NetworkAnalyzer;

public interface IHttpDownloader
{
    Task<byte[]> GetByteArrayAsync(string url);
}