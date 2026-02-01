using System.Net;
using System.Net.Http;
using Microsoft.Extensions.ObjectPool;

namespace OctaneEngineCore.Clients;

public class HTTPRequestMessagePoolPolicy: IPooledObjectPolicy<HttpRequestMessage>
{
    public HttpRequestMessage Create()
    {
        return new()
        {
            Method = HttpMethod.Get,
            VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
            Version = HttpVersion.Version20
        };
    }

    public bool Return(HttpRequestMessage message)
    {
        message.Headers.Clear();
        message.RequestUri = null;
        message.Content?.Dispose();
        message.Content = null;
        
        return true;
    }
}