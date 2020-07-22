using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OctaneDownloadEngine
{
    class HttpClientPool: IDisposable
    {
        private readonly ConcurrentBag<HttpClient> _objects;

        private HttpClient GenerateClient()
        {
            var client = new HttpClient(
                    //Use our custom Retry handler, with a max retry value of 10
                    new RetryHandler(new HttpClientHandler(), 10))
                { MaxResponseContentBufferSize = 1000000000 };

            client.DefaultRequestHeaders.ConnectionClose = false;
            client.Timeout = Timeout.InfiniteTimeSpan;

            return client;
        }

        public HttpClientPool(int number)
        {
            _objects = new ConcurrentBag<HttpClient>();
            Parallel.For(0, number, (i) =>
            {
                _objects.Add(GenerateClient());
            });
        }

        public HttpClient Get() => _objects.TryTake(out HttpClient item) ? item : GenerateClient();

        public void Return(HttpClient item) => _objects.Add(item);

        public void Dispose()
        {
            foreach (var client in _objects)
            {
                client.Dispose();
            }
        }
    }
}
