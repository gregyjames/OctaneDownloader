using System;
using System.IO.MemoryMappedFiles;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OctaneEngine;

public class DefaultClient: IClient
{
    private readonly HttpClient _httpClient;
    private readonly MemoryMappedFile _mmf;

    public DefaultClient(HttpClient httpClient, MemoryMappedFile mmf)
    {
        _httpClient = httpClient;
        _mmf = mmf;
    }
    public async Task<HttpResponseMessage> SendMessage(string url, (long, long) piece, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
    }

    public async Task ReadResponse(HttpResponseMessage message, (long, long) piece, CancellationToken cancellationToken)
    {
        using (var stream = _mmf.CreateViewStream())
        {
            await message.Content.CopyToAsync(stream);
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _mmf?.Dispose();
    }
}