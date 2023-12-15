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
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Net.Http;
using System.Threading;
using OctaneEngine;
using OctaneEngineCore.ShellProgressBar;
using PooledAwait;

namespace OctaneEngineCore.Clients;

internal class DefaultClient : IClient
{
    private readonly HttpClient _httpClient;
    private MemoryMappedFile _mmf;
    private ArrayPool<byte> _memPool;
    private readonly OctaneConfiguration _config;
    private readonly IProgressBar _pbar;
    private readonly long _partsize;

    public DefaultClient(HttpClient httpClient, OctaneConfiguration config, IProgressBar pbar, long partsize)
    {
        _httpClient = httpClient;
        _config = config;
        _pbar = pbar;
        _partsize = partsize;
    }

    public async PooledTask CopyMessageContentToStreamWithProgressAsync(HttpResponseMessage message, Stream stream, IProgress<long> progress)
    {
        var buffer = _memPool.Rent(_config.BufferSize);
        long totalBytesWritten = 0;

        using (var memoryStream = new MemoryStream())
        {
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
    }


    public bool isRangeSupported()
    {
        return false;
    }

    public void SetMMF(MemoryMappedFile file)
    {
        _mmf = file;
    }

    public void SetArrayPool(ArrayPool<byte> pool)
    {
        _memPool = pool;
    }

    public async PooledTask<HttpResponseMessage> SendMessage(string url, (long, long) piece,
        CancellationToken cancellationToken, PauseToken pauseToken)
    {
        if (pauseToken.IsPaused)
        {
            await pauseToken.WaitWhilePausedAsync().ConfigureAwait(false);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
    }

    public async PooledTask ReadResponse(HttpResponseMessage message, (long, long) piece, CancellationToken cancellationToken, PauseToken pauseToken)
    {
        if (pauseToken.IsPaused)
        {
            await pauseToken.WaitWhilePausedAsync().ConfigureAwait(false);
        }

        long totalWritten = 0;
        
        var progress = new System.Progress<long>(bytesWritten =>
        {
            totalWritten += bytesWritten;

            if (totalWritten % (_partsize / _config.BufferSize) == 0)
            {
                _pbar?.Tick();
            }
        });
        using var stream = _mmf.CreateViewStream();
        await CopyMessageContentToStreamWithProgressAsync(message, stream, progress);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _mmf?.Dispose();
        _pbar?.Dispose();
    }
}