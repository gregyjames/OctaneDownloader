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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using OctaneEngineCore.Implementations.NetworkAnalyzer;
using OctaneEngineCore.ShellProgressBar;
using OctaneEngineCore.Streams;

namespace OctaneEngineCore.Clients;

public partial class OctaneClient : IClient
{
    private const int LowMemoryMaxBufferSize = 64 * 1024; // 64 KB (below LOH threshold, L2-cache friendly)

    private readonly HttpClient _client;
    private readonly OctaneConfiguration _config;
    private readonly ILogger<IClient> _log;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ArrayPool<byte> _memPool = ArrayPool<byte>.Shared;
    private ProgressBar? _progressBar;
    private readonly long _tickStep;

    private static readonly ProgressBarOptions ChildProgressBarOptions = new()
    {
        CollapseWhenFinished = true,
        DisplayTimeInRealTime = false,
        BackgroundColor = ConsoleColor.Magenta,
        DenseProgressBar = true,
        DisableBottomPercentage = true,
        ShowEstimatedDuration = true
    };
    
    public OctaneClient(OctaneConfiguration config, HttpClient httpClient, ILoggerFactory loggerFactory, ProgressBar? progressBar = null)
    {
        _config = config;
        _client = httpClient;
        _loggerFactory = loggerFactory;
        _progressBar = progressBar;
        _log = loggerFactory.CreateLogger<IClient>();
        _tickStep = Math.Max(_config.BufferSize * 2L, 1 * 1024 * 1024L);
    }
    
    public bool IsRangeSupported() => true;

    public void SetProgressbar(ProgressBar bar) => _progressBar = bar;

    private async ValueTask<HttpResponseMessage> SendRangeRequestAsync(
        Uri uri, 
        (long start, long end) piece, 
        Dictionary<string, string>? headers, 
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new()
        {
            Method = HttpMethod.Get,
            VersionPolicy = _config.HttpVersionPolicy,
            Version = _config.HttpVersion ?? System.Net.HttpVersion.Version11,
            RequestUri = uri,
            Headers =
            {
                Range = new(piece.start, piece.end)
            }
        };
        
        if (headers != null)
        {
            foreach (var (key, value) in headers)
            {
                request.Headers.TryAddWithoutValidation(key, value);
            }
        }
            
        var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        
        return response;
    }
    
    public async Task Download(Uri uri, (long start, long end) piece, SafeFileHandle fileHandle, Dictionary<string, string>? headers, CancellationToken cancellationToken, PauseToken pauseToken)
    {
        ArgumentNullException.ThrowIfNull(fileHandle);

        if (pauseToken.IsPaused)
        {
            await pauseToken.WaitWhilePausedAsync(cancellationToken).ConfigureAwait(false);
        }

        LogSendingRequestForRangePieces(piece.start, piece.end);
        using var message = await SendRangeRequestAsync(uri, piece, headers, cancellationToken).ConfigureAwait(false);
        
        var programBps = _config.BytesPerSecond / _config.Parts;
        
        long startTimestamp = Stopwatch.GetTimestamp();
        if (message.IsSuccessStatusCode)
        {
            LogHttpRequestReturnedSuccessStatus((int)message.StatusCode, piece.start, piece.end);
            await using var networkStream = await message.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            ThrottleStream? throttleStream = null;
            Stream readStream = networkStream;

            if (_config.BytesPerSecond > 1)
            {
                throttleStream = new ThrottleStream(_loggerFactory);
                throttleStream.SetStreamParent(networkStream);
                throttleStream.SetBps(programBps);
                readStream = throttleStream;
            }

            try
            {
                // Only create child progress bar if ShowProgress is enabled, and we have a progress bar
                using var child = (_config.ShowProgress && _progressBar != null)
                    ? _progressBar.Spawn(piece.end - piece.start, "Downloading part...", ChildProgressBarOptions)
                    : null;

                if (_config.LowMemoryMode)
                {
                    await LowMemoryDownload(piece, fileHandle, cancellationToken, readStream, child, pauseToken).ConfigureAwait(false);
                }
                else
                {
                    await RegularDownload(piece, fileHandle, cancellationToken, readStream, child, pauseToken).ConfigureAwait(false);
                }
            }
            finally
            {
                if (throttleStream != null)
                {
                    await throttleStream.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
        else if (_log.IsEnabled(LogLevel.Error))
        {
            LogHttpRequestReturnedSuccessStatusCodeCode((int)message.StatusCode, NetworkAnalyzer.PrettySize(piece.start), NetworkAnalyzer.PrettySize(piece.end));
        }
        
        // Only tick the progress bar if ShowProgress is enabled
        if (_config.ShowProgress)
        {
            _progressBar?.Tick();
        }

        if (_log.IsEnabled(LogLevel.Information))
        {
            long elapsedMs = (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            LogPieceExecutionTimeElapsedMilliseconds(NetworkAnalyzer.PrettySize(piece.start), NetworkAnalyzer.PrettySize(piece.end), elapsedMs);
        }
    }

    private async Task RegularDownload(
        (long start, long end) piece,
        SafeFileHandle fileHandle,
        CancellationToken cancellationToken,
        Stream sourceStream,
        ChildProgressBar? child,
        PauseToken pauseToken)
    {
        int readBufferSize = Math.Max(_config.BufferSize, 256 * 1024);
        long pieceLength = piece.end - piece.start + 1;
        var readBuffer = _memPool.Rent(readBufferSize);
        var writeBuffer = _memPool.Rent(readBufferSize);

        if (_log.IsEnabled(LogLevel.Debug))
        {
            LogBufferRentedOfSize(readBufferSize, piece.start, piece.end);
        }

        ValueTask pendingWrite = ValueTask.CompletedTask;
        bool hasPendingWrite = false;

        try
        {
            long bytesReadOverall = 0;
            long lastProgressUpdate = 0;

            while (bytesReadOverall < pieceLength)
            {
                int filled = 0;
                int targetFill = (int)Math.Min(readBufferSize, pieceLength - bytesReadOverall);

                while (filled < targetFill)
                {
                    if (pauseToken.IsPaused)
                    {
                        await pauseToken.WaitWhilePausedAsync(cancellationToken).ConfigureAwait(false);
                    }

                    int bytesRead = await sourceStream.ReadAsync(
                        readBuffer.AsMemory(filled, targetFill - filled),
                        cancellationToken).ConfigureAwait(false);

                    if (bytesRead == 0)
                    {
                        break;
                    }

                    filled += bytesRead;
                }

                if (filled == 0)
                {
                    break;
                }

                // Wait for the previous overlapped disk write to complete before reusing writeBuffer
                if (hasPendingWrite)
                {
                    await pendingWrite.ConfigureAwait(false);
                    hasPendingWrite = false;
                }

                // Swap buffers so the next network read overlaps with this disk write
                (readBuffer, writeBuffer) = (writeBuffer, readBuffer);

                long writeOffset = piece.start + bytesReadOverall;
                pendingWrite = RandomAccess.WriteAsync(fileHandle, writeBuffer.AsMemory(0, filled), writeOffset, cancellationToken);
                hasPendingWrite = true;

                bytesReadOverall += filled;

                if (child != null && (bytesReadOverall - lastProgressUpdate >= _tickStep || bytesReadOverall >= pieceLength))
                {
                    child.Tick(bytesReadOverall);
                    lastProgressUpdate = bytesReadOverall;
                }
            }

            if (hasPendingWrite)
            {
                await pendingWrite.ConfigureAwait(false);
                hasPendingWrite = false;
            }
        }
        finally
        {
            if (hasPendingWrite)
            {
                try
                {
                    await pendingWrite.ConfigureAwait(false);
                }
                catch
                {
                    // Ensure in-flight write completes before returning the rented buffer
                }
            }

            _memPool.Return(readBuffer);
            _memPool.Return(writeBuffer);
            LogBufferReturnedToMemoryPool();
        }
    }

    private async Task LowMemoryDownload(
        (long start, long end) piece,
        SafeFileHandle fileHandle,
        CancellationToken cancellationToken,
        Stream sourceStream,
        ChildProgressBar? child,
        PauseToken pauseToken)
    {
        int readBufferSize = Math.Min(_config.BufferSize, LowMemoryMaxBufferSize);
        long pieceLength = piece.end - piece.start + 1;
        var readBuffer = _memPool.Rent(readBufferSize);

        if (_log.IsEnabled(LogLevel.Debug))
        {
            LogBufferRentedOfSize(readBufferSize, piece.start, piece.end);
        }

        try
        {
            long bytesReadOverall = 0;
            long lastProgressUpdate = 0;

            while (bytesReadOverall < pieceLength)
            {
                if (pauseToken.IsPaused)
                {
                    await pauseToken.WaitWhilePausedAsync(cancellationToken).ConfigureAwait(false);
                }

                int maxToRead = (int)Math.Min(readBufferSize, pieceLength - bytesReadOverall);
                int bytesRead = await sourceStream.ReadAsync(readBuffer.AsMemory(0, maxToRead), cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                }

                await RandomAccess.WriteAsync(fileHandle, readBuffer.AsMemory(0, bytesRead), piece.start + bytesReadOverall, cancellationToken).ConfigureAwait(false);
                bytesReadOverall += bytesRead;

                if (child != null && (bytesReadOverall - lastProgressUpdate >= _tickStep || bytesReadOverall >= pieceLength))
                {
                    child.Tick(bytesReadOverall);
                    lastProgressUpdate = bytesReadOverall;
                }
            }
        }
        finally
        {
            _memPool.Return(readBuffer);
            LogBufferReturnedToMemoryPool();
        }
    }

    [LoggerMessage(LogLevel.Trace, "Sending request for range ({pieceItem1},{pieceItem2})...")]
    partial void LogSendingRequestForRangePieces(long pieceItem1, long pieceItem2);

    [LoggerMessage(LogLevel.Debug, "HTTP request returned success status code {code} for piece ({pieceItem1:N0}, {pieceItem2:N0})")]
    partial void LogHttpRequestReturnedSuccessStatus(int code, long pieceItem1, long pieceItem2);

    [LoggerMessage(LogLevel.Debug, "Buffer rented of size {configBufferSize} for piece ({pieceItem1},{pieceItem2})")]
    partial void LogBufferRentedOfSize(int configBufferSize, long pieceItem1, long pieceItem2);

    [LoggerMessage(LogLevel.Information, "Buffer returned to memory pool")]
    partial void LogBufferReturnedToMemoryPool();

    [LoggerMessage(LogLevel.Error, "HTTP request returned success status code {code} for piece ({pieceItem1},{pieceItem2})")]
    partial void LogHttpRequestReturnedSuccessStatusCodeCode(int code, string pieceItem1, string pieceItem2);

    [LoggerMessage(LogLevel.Information, "Piece ({pieceItem1},{pieceItem2}) finished in {stopwatchElapsedMilliseconds:N0}ms.")]
    partial void LogPieceExecutionTimeElapsedMilliseconds(string pieceItem1, string pieceItem2, long stopwatchElapsedMilliseconds);
}
