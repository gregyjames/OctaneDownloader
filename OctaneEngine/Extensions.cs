#if NET461 || NET472
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OctaneEngine
{
    public static class Extensions
    {
        public static Task<Stream> ReadAsStreamAsync(this HttpContent httpContent, CancellationToken cancellationToken)
        {
            return httpContent.ReadAsStreamAsync();
        }

    }
}
#endif