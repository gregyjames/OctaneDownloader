using System;
using System.Net.Http;

namespace OctaneEngineCore.Clients;

public class SingleHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient _client;

    public SingleHttpClientFactory(HttpClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public HttpClient CreateClient(string name)
    {
        return _client;
    }
}
