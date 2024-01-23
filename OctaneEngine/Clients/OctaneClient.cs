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
using Microsoft.Extensions.Logging;
using OctaneEngine;
using OctaneEngineCore.ShellProgressBar;
using OctaneEngineCore.Streams;
using PooledAwait;

// ReSharper disable TemplateIsNotCompileTimeConstantProblem

namespace OctaneEngineCore.Clients;
internal class OctaneClient : IClient
{
    private readonly HttpClient _client;
    private readonly OctaneConfiguration _config;
    private readonly ILogger<IClient> _log;
    private readonly ILoggerFactory _loggerFactory;
    private ArrayPool<byte> _memPool;
    private MemoryMappedFile _mmf;
    private ProgressBar _progressBar;

    public OctaneClient(OctaneConfiguration config, HttpClient httpClient, ILoggerFactory loggerFactory, ProgressBar progressBar = null)
    {
        _config = config;
        _client = httpClient;
        _loggerFactory = loggerFactory;
        _progressBar = progressBar;
        _log = loggerFactory.CreateLogger<IClient>();
    }

    public void SetBaseAddress(string url)
    {
        var basePart = new Uri(new Uri(url).GetLeftPart(UriPartial.Authority));
        _client.BaseAddress = basePart;
    }
    
    public bool IsRangeSupported()
    {
        return true;
    }

    public void SetMmf(MemoryMappedFile file)
    {
        _mmf = file;
    }

    public void SetProgressbar(ProgressBar bar)
    {
        _progressBar = bar;
    }
    public void SetArrayPool(ArrayPool<byte> pool)
    {
        _memPool = pool;
    }
    
    public async PooledTask Download(string url,(long, long) piece, CancellationToken cancellationToken, PauseToken pauseToken)
    {
        _log.LogTrace("Sending request for range ({PieceItem1},{PieceItem2})...", piece.Item1, piece.Item2);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
        request.Headers.Range = new RangeHeaderValue(piece.Item1, piece.Item2);
        var message = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        
        #region Variable Declaration
        var readbuffer = _memPool.Rent(_config.BufferSize);
        _log.LogInformation("Buffer rented of size {ConfigBufferSize} for piece ({PieceItem1},{PieceItem2})", _config.BufferSize, piece.Item1, piece.Item2);
        var stopwatch = new Stopwatch();
        var programBps = _config.BytesPerSecond / _config.Parts;
        long bytesReadOverall = 0;
        var childOptions = new ProgressBarOptions()
        {
            CollapseWhenFinished = true,
            DisplayTimeInRealTime = false,
            BackgroundColor = ConsoleColor.Magenta,
            DenseProgressBar = true,
            DisableBottomPercentage = true,
            ShowEstimatedDuration = true
        };
        #endregion
        
        if (pauseToken.IsPaused)
        {
            await pauseToken.WaitWhilePausedAsync().ConfigureAwait(false);
        }
        
        stopwatch.Start();
        if (message.IsSuccessStatusCode)
        {
            _log.LogInformation("HTTP request returned success status code for piece ({PieceItem1},{PieceItem2})", piece.Item1, piece.Item2);
            await using (var networkStream = await message.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            {
                IStream _stream = _config.BytesPerSecond <= 1 ? new NormalStream() : new ThrottleStream(_loggerFactory);
                _stream.SetStreamParent(networkStream);
                _stream.SetBps(programBps);

                using var child = _progressBar?.Spawn(Convert.ToInt32(piece.Item2 - piece.Item1), "Downloading part...", childOptions);

                if (_config.LowMemoryMode)
                {
                    try
                    {
                        while (true)
                        {
                            var bytesRead = await _stream.ReadAsync(readbuffer.AsMemory(), cancellationToken);

                            if (bytesRead == 0)
                            {
                                break;
                            }
                            
                            using (var accessor = _mmf.CreateViewAccessor(piece.Item1 + bytesReadOverall, bytesRead,
                                       MemoryMappedFileAccess.Write))
                            {
                                accessor.WriteArray(0, readbuffer, 0, bytesRead);
                                bytesReadOverall += bytesRead;
                                child?.Tick((int)bytesReadOverall);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex.Message);
                        throw;
                    }
                }
                else
                {
                    var stream = _mmf.CreateViewStream(piece.Item1, 0);
                    try
                    {
                        while (true)
                        {
                            var bytesRead = await _stream.ReadAsync(readbuffer.AsMemory(), cancellationToken);
                            if (bytesRead == 0)
                            {
                                break;
                            }
                            await stream.WriteAsync(readbuffer.AsMemory().Slice(0, bytesRead), cancellationToken);
                            bytesReadOverall += bytesRead;
                            child?.Tick((int)bytesReadOverall);
                        }
                    }
                    finally
                    {
                        // Flush and dispose of the MemoryMappedViewStream
                        await stream.FlushAsync(cancellationToken);
                        await stream.DisposeAsync();
                        await _stream.DisposeAsync();
                    }
                }

            }

            _log.LogInformation("Buffer returned to memory pool");
        }
        _memPool.Return(readbuffer);
        _progressBar?.Tick();
        _log.LogInformation("Piece ({PieceItem1},{PieceItem2}) done", piece.Item1, piece.Item2);
        stopwatch.Stop();
        _log.LogInformation("Piece ({PieceItem1},{PieceItem2}) finished in {StopwatchElapsedMilliseconds} ms", piece.Item1, piece.Item2, stopwatch.ElapsedMilliseconds);
    }

    public void Dispose()
    {
        //_client?.Dispose();
        //_loggerFactory?.Dispose();
        //_mmf?.Dispose();
        //_progressBar?.Dispose();
    }
}