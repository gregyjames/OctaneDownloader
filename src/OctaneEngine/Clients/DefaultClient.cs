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
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using OctaneEngineCore.ShellProgressBar;

namespace OctaneEngineCore.Clients;

public class DefaultClient : IClient
{
    private ProgressBar? _pBar;
    private readonly HttpClient _httpClient;
    private readonly OctaneConfiguration _config;

    public DefaultClient(HttpClient httpClient, OctaneConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    public bool IsRangeSupported() => false;

    public void SetProgressbar(ProgressBar bar)
    {
        _pBar = bar;
    }

    public async Task Download(Uri uri, (long, long) piece, SafeFileHandle fileHandle, Dictionary<string, string>? headers, CancellationToken cancellationToken, PauseToken pauseToken)
    {
        ArgumentNullException.ThrowIfNull(fileHandle);

        if (pauseToken.IsPaused)
        {
            await pauseToken.WaitWhilePausedAsync(cancellationToken).ConfigureAwait(false);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, uri)
        {
            Version = _config.HttpVersion ?? System.Net.HttpVersion.Version11,
            VersionPolicy = _config.HttpVersionPolicy,
        };
        if (headers != null)
        {
            foreach (var header in headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        using var message = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        await WriteResponseAsync(message, fileHandle, cancellationToken, pauseToken).ConfigureAwait(false);
    }

    internal async Task WriteResponseAsync(
        HttpResponseMessage message,
        SafeFileHandle fileHandle,
        CancellationToken cancellationToken,
        PauseToken pauseToken)
    {
        message.EnsureSuccessStatusCode();

        await using var contentStream = await message.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        bool trackProgress = _config.ShowProgress && _pBar != null;
        if (_config.LowMemoryMode)
        {
            await WriteLowMemoryAsync(contentStream, fileHandle, trackProgress, cancellationToken, pauseToken).ConfigureAwait(false);
        }
        else
        {
            await WriteRegularAsync(contentStream, fileHandle, trackProgress, cancellationToken, pauseToken).ConfigureAwait(false);
        }
    }

    private async Task WriteRegularAsync(
        Stream contentStream,
        SafeFileHandle fileHandle,
        bool trackProgress,
        CancellationToken cancellationToken,
        PauseToken pauseToken)
    {
        int bufferSize = Math.Max(_config.BufferSize, 256 * 1024);
        var readBuffer = GC.AllocateUninitializedArray<byte>(bufferSize, pinned: true);
        var writeBuffer = GC.AllocateUninitializedArray<byte>(bufferSize, pinned: true);

        long totalWritten = 0;
        long lastProgressUpdate = 0;
        long progressStep = Math.Max(bufferSize * 4L, 1024 * 1024L);

        ValueTask pendingWrite = ValueTask.CompletedTask;
        bool hasPendingWrite = false;

        try
        {
            while (true)
            {
                if (pauseToken.IsPaused)
                {
                    await pauseToken.WaitWhilePausedAsync(cancellationToken).ConfigureAwait(false);
                }

                int bytesRead = await contentStream.ReadAsync(readBuffer.AsMemory(0, bufferSize), cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                }

                if (hasPendingWrite)
                {
                    await pendingWrite.ConfigureAwait(false);
                    hasPendingWrite = false;
                }

                (readBuffer, writeBuffer) = (writeBuffer, readBuffer);

                pendingWrite = RandomAccess.WriteAsync(fileHandle, writeBuffer.AsMemory(0, bytesRead), totalWritten, cancellationToken);
                hasPendingWrite = true;
                totalWritten += bytesRead;

                if (trackProgress && totalWritten - lastProgressUpdate >= progressStep)
                {
                    _pBar!.Tick();
                    lastProgressUpdate = totalWritten;
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
                    // Ensure in-flight write completes before releasing pinned buffers
                }
            }
        }
    }

    private async Task WriteLowMemoryAsync(
        Stream contentStream,
        SafeFileHandle fileHandle,
        bool trackProgress,
        CancellationToken cancellationToken,
        PauseToken pauseToken)
    {
        int bufferSize = Math.Min(_config.BufferSize, 64 * 1024);
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        long totalWritten = 0;
        long lastProgressUpdate = 0;
        long progressStep = Math.Max(bufferSize * 4L, 1024 * 1024L);

        try
        {
            while (true)
            {
                if (pauseToken.IsPaused)
                {
                    await pauseToken.WaitWhilePausedAsync(cancellationToken).ConfigureAwait(false);
                }

                int bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, bufferSize), cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                }

                await RandomAccess.WriteAsync(fileHandle, buffer.AsMemory(0, bytesRead), totalWritten, cancellationToken).ConfigureAwait(false);
                totalWritten += bytesRead;

                if (trackProgress && totalWritten - lastProgressUpdate >= progressStep)
                {
                    _pBar!.Tick();
                    lastProgressUpdate = totalWritten;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _pBar?.Dispose();
    }
}
