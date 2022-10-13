using System;
using System.Buffers;
using System.IO.MemoryMappedFiles;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using OctaneEngine.ShellProgressBar;

namespace OctaneEngine;

public class OctaneClient
{
    private readonly HttpClient _client;
    private readonly OctaneConfiguration _config;
    
    private readonly ProgressBarOptions _childOptions = new ProgressBarOptions
    {
        ForegroundColor = ConsoleColor.Cyan,
        BackgroundColor = ConsoleColor.Black,
        CollapseWhenFinished = true,
        DenseProgressBar = true,
        BackgroundCharacter = '\u2591',
        DisplayTimeInRealTime = false
    };
    
    public OctaneClient(OctaneConfiguration config, HttpClient httpClient)
    {
        _config = config;
        _client = httpClient;
    }

    public async Task<HttpResponseMessage> SendMessage(string url, (long, long) piece, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
        request.Headers.Range = new RangeHeaderValue(piece.Item1, piece.Item2);
        return await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
    }

    public async Task ReadResponse(HttpResponseMessage message, MemoryMappedFile mmf, (long, long) piece, ArrayPool<byte> memPool, CancellationToken cancellationToken, ProgressBar progressBar)
    {
        var programBps = _config.BytesPerSecond / _config.Parts;
        
        if (message.IsSuccessStatusCode)
        {
            //Get the content stream from the message request
            using var streamToRead = await message.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            //Throttle stream to over BPS divided among the parts
            var source = _config.BytesPerSecond != 1 ? new ThrottleStream(streamToRead, programBps) : null;
            //Create a memory mapped stream to the mmf with the piece offset and size equal to the response size
            using var streams = mmf.CreateViewStream(piece.Item1, message.Content.Headers.ContentLength!.Value, MemoryMappedFileAccess.Write);
            var child = progressBar?.Spawn((int)Math.Round((double)(message.Content.Headers.ContentLength / _config.BufferSize)), "", _childOptions);
            
            //Copy from the content stream to the mmf stream
            var buffer = memPool.Rent(_config.BufferSize);

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
                child?.Tick();
            } while (bytesRead != 0);
                        
            memPool.Return(buffer);
            
            child?.Dispose();
            streams.Flush();
        }

        progressBar?.Tick();
    }
}