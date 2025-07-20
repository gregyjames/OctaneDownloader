
using System.Threading.Tasks;

public interface IHttpDownloader
{
    Task<byte[]> GetByteArrayAsync(string url);
}