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
using System.IO.MemoryMappedFiles;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IO;
using OctaneEngineCore.ShellProgressBar;

namespace OctaneEngineCore.Clients;

public class DefaultClient(HttpClient httpClient, OctaneConfiguration config)
    : IClient
{
    private MemoryMappedFile _mmf;
    private ArrayPool<byte> _memPool;
    private ProgressBar _pBar;

    public bool IsRangeSupported()
    {
        return false;
    }

    public void SetMmf(MemoryMappedFile file)
    {
        _mmf = file;
    }

    public void SetProgressbar(ProgressBar bar)
    {
        _pBar = bar;
    }

    public void SetArrayPool(ArrayPool<byte> pool)
    {
        _memPool = pool;
    }
    
    private static readonly RecyclableMemoryStreamManager _streamManager = new();
    private async Task CopyMessageContentToStreamWithProgressAsync(HttpResponseMessage message, Stream stream, IProgress<long> progress)
    {
        byte[] buffer = _memPool.Rent(config.BufferSize);
        long totalBytesWritten = 0;

        using MemoryStream memoryStream = _streamManager.GetStream("OctaneEngineCore-DefaultClient-CopyMessageContentToStreamWithProgressAsync");
        await message.Content.CopyToAsync(memoryStream);

        memoryStream.Seek(0, SeekOrigin.Begin);

        int bytesRead;
        while ((bytesRead = await memoryStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await stream.WriteAsync(buffer, 0, bytesRead);

            totalBytesWritten += bytesRead;

            if (progress != null)
            {
                progress.Report(totalBytesWritten);
            }
        }
    }
    
    public async Task Download(string url, (long, long) piece, Dictionary<string, string> headers, CancellationToken cancellationToken, PauseToken pauseToken)
    {
        if (pauseToken.IsPaused)
        {
            await pauseToken.WaitWhilePausedAsync().ConfigureAwait(false);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
        if (headers != null)
        {
            foreach (var header in headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }
        }

        var message = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        
        if (pauseToken.IsPaused)
        {
            await pauseToken.WaitWhilePausedAsync().ConfigureAwait(false);
        }

        long totalWritten = 0;
        
        var progress = new System.Progress<long>(bytesWritten =>
        {
            totalWritten += bytesWritten;

            // Only update progress bar if ShowProgress is enabled
            if (config.ShowProgress && totalWritten % ((piece.Item2-piece.Item1) / config.BufferSize) == 0)
            {
                _pBar?.Tick();
            }
        });
        await using var stream = _mmf.CreateViewStream();
        await CopyMessageContentToStreamWithProgressAsync(message, stream, progress);
    }

    public void Dispose()
    {
        httpClient?.Dispose();
        _mmf?.Dispose();
        _pBar?.Dispose();
    }
}