using System;
using System.Buffers;
using System.IO.MemoryMappedFiles;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OctaneEngine.ShellProgressBar;

namespace OctaneEngine;

public class OctaneClient
{
    private readonly HttpClient _client;
    private readonly OctaneConfiguration _config;
    private readonly ILogger<OctaneClient> _log;
    
    private readonly ProgressBarOptions _childOptions = new ProgressBarOptions
    {
        ForegroundColor = ConsoleColor.Cyan,
        BackgroundColor = ConsoleColor.Black,
        CollapseWhenFinished = true,
        DenseProgressBar = true,
        BackgroundCharacter = '\u2591',
        DisplayTimeInRealTime = false
    };

    public OctaneClient(OctaneConfiguration config, HttpClient httpClient, ILoggerFactory loggerFactory)
    {
        _config = config;
        _client = httpClient;
        _log = loggerFactory.CreateLogger<OctaneClient>();
    }

    public async Task<HttpResponseMessage> SendMessage(string url, (long, long) piece, CancellationToken cancellationToken)
    {
        _log.LogTrace($"Sending request for range ({piece.Item1},{piece.Item2})...");
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
        request.Headers.Range = new RangeHeaderValue(piece.Item1, piece.Item2);
        return await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
    }

    public async Task ReadResponse(HttpResponseMessage message, MemoryMappedFile mmf, (long, long) piece,
        ArrayPool<byte> memPool, CancellationToken cancellationToken, ProgressBar progressBar,
        ILoggerFactory loggerFactory)
    {
        var stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        var programBps = _config.BytesPerSecond / _config.Parts;
        
        if (message.IsSuccessStatusCode)
        {
            _log.LogInformation($"HTTP request returned success status code for piece ({piece.Item1},{piece.Item2}).");
            //Get the content stream from the message request
            using var streamToRead = await message.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            //Throttle stream to over BPS divided among the parts
            var source = _config.BytesPerSecond != 1 ? new ThrottleStream(streamToRead, programBps, loggerFactory) : null;
            if (source != null)
            {
                _log.LogTrace($"Throttling stream for piece ({piece.Item1},{piece.Item2}) to {programBps} bytes per second.");
            }
            
            //Create a memory mapped stream to the mmf with the piece offset and size equal to the response size
            using var streams = mmf.CreateViewStream(piece.Item1, message.Content.Headers.ContentLength!.Value, MemoryMappedFileAccess.Write);
            var child = progressBar?.Spawn((int)Math.Round((double)(message.Content.Headers.ContentLength / _config.BufferSize)), "", _childOptions);
            
            //Copy from the content stream to the mmf stream
            var buffer = memPool.Rent(_config.BufferSize);
            _log.LogInformation($"Buffer rented of size {_config.BufferSize} for piece ({piece.Item1},{piece.Item2}).");
            
            int bytesRead;
            // Until we've read everything
            do
            {
                var offset = 0;
                // Until the buffer is very nearly full or there's nothing left to read
                do {
                    if (_config.BytesPerSecond == 1)
                    {
                        bytesRead = await streamToRead.ReadAsync(buffer, offset, _config.BufferSize - offset, cancellationToken).ConfigureAwait(false);
                        offset += bytesRead;
                        _log.LogTrace($"Read {offset} bytes from piece ({piece.Item1},{piece.Item2}).");
                    }
                    else
                    {
                        if (source != null)
                        {
                            bytesRead = await source.ReadAsync(buffer, offset, _config.BufferSize - offset, cancellationToken).ConfigureAwait(false);
                            offset += bytesRead;
                        }
                        else
                        {
                            bytesRead = 0;
                            Console.WriteLine("Error reading stream!");
                        }
                    }
                } while (bytesRead != 0 && offset < _config.BufferSize);

                // Empty the buffer
                if (offset == 0) continue;
                await streams.WriteAsync(buffer, 0, offset, cancellationToken).ConfigureAwait(false);
                _log.LogTrace($"Wrote from stream to buffer for piece ({piece.Item1},{piece.Item2})");
                child?.Tick();
            } while (bytesRead != 0);
                        
            memPool.Return(buffer);
            _log.LogInformation("Buffer returned to memory pool.");
            
            child?.Dispose();
            streams.Flush();
        }

        progressBar?.Tick();
        _log.LogInformation($"Piece ({piece.Item1},{piece.Item2}) done.");
        stopwatch.Stop();
        
        _log.LogInformation($"Piece ({piece.Item1},{piece.Item2}) finished in {stopwatch.ElapsedMilliseconds} ms.");
    }
}