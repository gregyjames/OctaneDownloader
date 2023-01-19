/*
 * The MIT License (MIT)
 * Copyright (c) 2015 Greg James
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NON-INFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OctaneEngineCore;
using OctaneEngineCore.ShellProgressBar;

namespace OctaneEngine;
public class OctaneClient : IClient
{
    private readonly ProgressBarOptions _childOptions = new()
    {
        ForegroundColor = ConsoleColor.Cyan,
        BackgroundColor = ConsoleColor.Black,
        CollapseWhenFinished = true,
        DenseProgressBar = true,
        BackgroundCharacter = '\u2591',
        DisplayTimeInRealTime = false
    };

    private readonly HttpClient _client;
    private readonly OctaneConfiguration _config;
    private readonly ILogger<IClient> _log;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ArrayPool<byte> _memPool;
    private readonly MemoryMappedFile _mmf;
    private readonly ProgressBar _progressBar;

    public OctaneClient(OctaneConfiguration config, HttpClient httpClient, ILoggerFactory loggerFactory,
        MemoryMappedFile mmf, ProgressBar progressBar, ArrayPool<byte> memPool)
    {
        _config = config;
        _client = httpClient;
        _loggerFactory = loggerFactory;
        _mmf = mmf;
        _progressBar = progressBar;
        _memPool = memPool;
        _log = loggerFactory.CreateLogger<IClient>();
    }

    public async Task<HttpResponseMessage> SendMessage(string url, (long, long) piece,
        CancellationToken cancellationToken, PauseToken pauseToken)
    {
        if (pauseToken.IsPaused)
        {
            await pauseToken.WaitWhilePausedAsync().ConfigureAwait(false);
        }
        _log.LogTrace("Sending request for range ({PieceItem1},{PieceItem2})...", piece.Item1, piece.Item2);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
        request.Headers.Range = new RangeHeaderValue(piece.Item1, piece.Item2);
        return await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ReadResponse(HttpResponseMessage message, (long, long) piece, CancellationToken cancellationToken, PauseToken pauseToken)
    {
        if (pauseToken.IsPaused)
        {
            await pauseToken.WaitWhilePausedAsync().ConfigureAwait(false);
        }
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        var programBps = _config.BytesPerSecond / _config.Parts;

        if (message.IsSuccessStatusCode)
        {
            _log.LogInformation("HTTP request returned success status code for piece ({PieceItem1},{PieceItem2})", piece.Item1, piece.Item2);
            //Get the content stream from the message request
            using var streamToRead = await message.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            //Throttle stream to over BPS divided among the parts
            var source = _config.BytesPerSecond != 1
                ? new ThrottleStream(streamToRead, programBps, _loggerFactory)
                : null;
            if (source != null)
                _log.LogTrace("Throttling stream for piece ({PieceItem1},{PieceItem2}) to {ProgramBps} bytes per second", piece.Item1, piece.Item2, programBps);

            //Create a memory mapped stream to the mmf with the piece offset and size equal to the response size
            using var streams = _mmf.CreateViewStream(piece.Item1, message.Content.Headers.ContentLength!.Value,
                MemoryMappedFileAccess.Write);
            var child = _progressBar?.Spawn(
                (int)Math.Round((double)(message.Content.Headers.ContentLength / _config.BufferSize)), "",
                _childOptions);

            //Copy from the content stream to the mmf stream
            var buffer = _memPool.Rent(_config.BufferSize);
            _log.LogInformation("Buffer rented of size {ConfigBufferSize} for piece ({PieceItem1},{PieceItem2})", _config.BufferSize, piece.Item1, piece.Item2);

            int bytesRead;
            // Until we've read everything
            do
            {
                var offset = 0;
                // Until the buffer is very nearly full or there's nothing left to read
                do
                {
                    if (_config.BytesPerSecond == 1)
                    {
                        bytesRead = await streamToRead
                            .ReadAsync(buffer, offset, _config.BufferSize - offset, cancellationToken)
                            .ConfigureAwait(false);
                        offset += bytesRead;
                        _log.LogTrace("Read {Offset} bytes from piece ({PieceItem1},{PieceItem2})", offset, piece.Item1, piece.Item2);
                    }
                    else
                    {
                        if (source != null)
                        {
                            bytesRead = await source
                                .ReadAsync(buffer, offset, _config.BufferSize - offset, cancellationToken)
                                .ConfigureAwait(false);
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
                _log.LogTrace("Wrote from stream to buffer for piece ({PieceItem1},{PieceItem2})", piece.Item1, piece.Item2);
                child?.Tick();
            } while (bytesRead != 0);

            _memPool.Return(buffer);
            _log.LogInformation("Buffer returned to memory pool");

            child?.Dispose();
            streams.Flush();
        }

        _progressBar?.Tick();
        _log.LogInformation("Piece ({PieceItem1},{PieceItem2}) done", piece.Item1, piece.Item2);
        stopwatch.Stop();

        _log.LogInformation("Piece ({PieceItem1},{PieceItem2}) finished in {StopwatchElapsedMilliseconds} ms", piece.Item1, piece.Item2, stopwatch.ElapsedMilliseconds);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _loggerFactory?.Dispose();
        _mmf?.Dispose();
        _progressBar?.Dispose();
    }
}