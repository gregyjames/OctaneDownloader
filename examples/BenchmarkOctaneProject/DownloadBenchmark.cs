using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OctaneEngineCore;
using OctaneEngineCore.Clients;
using OctaneEngineCore.Implementations;

namespace BenchmarkOctaneProject
{
    [MemoryDiagnoser]
    public class DownloadBenchmark
    {
        private byte[] _testData = null!;
        private string _tempOutputFile = null!;
        private HttpClient _mockHttpClient = null!;
        private Engine _engine = null!;
        private OctaneRequest _request = null!;

        [GlobalSetup]
        public void Setup()
        {
            // 20 MB test file
            _testData = new byte[20 * 1024 * 1024];
            new Random(42).NextBytes(_testData);
            _tempOutputFile = Path.Combine(Path.GetTempPath(), "benchmark_out.bin");

            var handler = new ZeroAllocationMockHttpMessageHandler(_testData);
            _mockHttpClient = new HttpClient(handler);

            var config = new OctaneConfiguration
            {
                Parts = 8,
                BufferSize = 64 * 1024,
                ShowProgress = false
            };

            _engine = new Engine(config, _mockHttpClient, NullLoggerFactory.Instance);
            _request = new OctaneRequest("http://localhost/testfile.bin", _tempOutputFile);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _mockHttpClient?.Dispose();
            _engine?.Dispose();
            if (File.Exists(_tempOutputFile))
            {
                File.Delete(_tempOutputFile);
            }
        }

        [Benchmark]
        public async Task DownloadFileBenchmark()
        {
            await _engine.DownloadFile(_request);
        }
    }

    internal class ZeroAllocationMockHttpMessageHandler : HttpMessageHandler
    {
        private readonly byte[] _fileData;

        public ZeroAllocationMockHttpMessageHandler(byte[] fileData)
        {
            _fileData = fileData;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage();

            if (request.Method == HttpMethod.Head)
            {
                response.StatusCode = HttpStatusCode.OK;
                response.Content = new ByteArrayContent(Array.Empty<byte>());
                response.Content.Headers.ContentLength = _fileData.Length;
                response.Headers.AcceptRanges.Add("bytes");
                return Task.FromResult(response);
            }

            if (request.Method == HttpMethod.Get)
            {
                var range = request.Headers.Range;
                if (range != null)
                {
                    var item = range.Ranges.GetEnumerator();
                    item.MoveNext();
                    var rangeItem = item.Current;
                    long start = rangeItem.From ?? 0;
                    long end = rangeItem.To ?? (_fileData.Length - 1);

                    long length = end - start + 1;
                    var ms = new MemoryStream(_fileData, (int)start, (int)length, writable: false);

                    response.StatusCode = HttpStatusCode.PartialContent;
                    response.Content = new StreamContent(ms);
                    response.Content.Headers.ContentLength = length;
                    response.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, end, _fileData.Length);
                }
                else
                {
                    response.StatusCode = HttpStatusCode.OK;
                    response.Content = new StreamContent(new MemoryStream(_fileData, 0, _fileData.Length, writable: false));
                    response.Content.Headers.ContentLength = _fileData.Length;
                }
                return Task.FromResult(response);
            }

            response.StatusCode = HttpStatusCode.BadRequest;
            return Task.FromResult(response);
        }
    }
}
