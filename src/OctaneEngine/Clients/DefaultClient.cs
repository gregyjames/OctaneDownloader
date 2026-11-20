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

    public async Task Download(string url, (long, long) piece, SafeFileHandle fileHandle, Dictionary<string, string>? headers, CancellationToken cancellationToken, PauseToken pauseToken)
    {
        ArgumentNullException.ThrowIfNull(fileHandle);

        if (pauseToken.IsPaused)
        {
            await pauseToken.WaitWhilePausedAsync(cancellationToken).ConfigureAwait(false);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url))
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
        message.EnsureSuccessStatusCode();

        await using var contentStream = await message.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        int bufferSize = _config.LowMemoryMode
            ? Math.Min(_config.BufferSize, 64 * 1024)
            : Math.Max(_config.BufferSize, 256 * 1024);

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

                if (_config.ShowProgress && _pBar != null && totalWritten - lastProgressUpdate >= progressStep)
                {
                    _pBar.Tick();
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
