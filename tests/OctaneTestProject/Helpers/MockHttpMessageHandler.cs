using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace OctaneTestProject;

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly byte[] _fileData;
    private readonly bool _supportsRange;
    private readonly bool _shouldFail;

    public MockHttpMessageHandler(byte[] fileData, bool supportsRange = true, bool shouldFail = false)
    {
        _fileData = fileData;
        _supportsRange = supportsRange;
        _shouldFail = shouldFail;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_shouldFail)
        {
            throw new HttpRequestException("Mocked connection failure.");
        }

        if (request.RequestUri != null && request.RequestUri.AbsolutePath.Contains("delay"))
        {
            var segments = request.RequestUri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            int delaySeconds = 1;
            foreach (var segment in segments)
            {
                if (int.TryParse(segment, out var d))
                {
                    delaySeconds = d;
                    break;
                }
            }
            // Use Task.Delay with the cancellationToken to support cancellation tests
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
        }

        var response = new HttpResponseMessage();

        if (request.Method == HttpMethod.Head)
        {
            response.StatusCode = HttpStatusCode.OK;
            response.Content = new ByteArrayContent(Array.Empty<byte>());
            response.Content.Headers.ContentLength = _fileData.Length;
            if (_supportsRange)
            {
                response.Headers.AcceptRanges.Add("bytes");
            }
            return response;
        }

        if (request.Method == HttpMethod.Get)
        {
            var range = request.Headers.Range;
            if (range != null && _supportsRange)
            {
                var item = range.Ranges.GetEnumerator();
                item.MoveNext();
                var rangeItem = item.Current;
                long start = rangeItem.From ?? 0;
                long end = rangeItem.To ?? (_fileData.Length - 1);

                long length = end - start + 1;
                var chunk = new byte[length];
                Array.Copy(_fileData, start, chunk, 0, length);

                response.StatusCode = HttpStatusCode.PartialContent;
                response.Content = new ByteArrayContent(chunk);
                response.Content.Headers.ContentLength = length;
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, end, _fileData.Length);
            }
            else
            {
                response.StatusCode = HttpStatusCode.OK;
                response.Content = new ByteArrayContent(_fileData);
                response.Content.Headers.ContentLength = _fileData.Length;
            }
            return response;
        }

        response.StatusCode = HttpStatusCode.BadRequest;
        return response;
    }
}
