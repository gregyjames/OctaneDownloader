using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OctaneEngine;

public interface IClient: IDisposable
{
    public Task<HttpResponseMessage> SendMessage(string url, (long, long) piece, CancellationToken cancellationToken);

    public Task ReadResponse(HttpResponseMessage message, (long, long) piece, CancellationToken cancellationToken);
}