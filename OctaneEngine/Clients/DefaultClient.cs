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
using System.IO.Pipelines;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using OctaneEngineCore.ShellProgressBar;

namespace OctaneEngineCore.Clients;

public class DefaultClient : IClient
{
    private MemoryMappedFile _mmf;
    private ArrayPool<byte> _memPool;
    private ProgressBar _pBar;
    private readonly HttpClient _httpClient;
    private readonly OctaneConfiguration _config;
    private readonly PipeOptions _pipeOptions;

    public DefaultClient(HttpClient httpClient, OctaneConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
        _pipeOptions = new PipeOptions(
            pool: MemoryPool<byte>.Shared,           
            readerScheduler: PipeScheduler.ThreadPool, 
            writerScheduler: PipeScheduler.ThreadPool, 
            pauseWriterThreshold: Math.Max(config.BufferSize * 4, 512 * 1024),
            resumeWriterThreshold: Math.Max(config.BufferSize * 2, 256*1024),
            minimumSegmentSize: Math.Max(config.BufferSize, 128*1024),
            useSynchronizationContext: false
        );
    }

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
    
    private async Task CopyMessageContentToStreamWithProgressAsync(
        HttpResponseMessage message, 
        Stream stream, 
        IProgress<long> progress)
    {
        await using var contentStream = await message.Content.ReadAsStreamAsync();

        var pipe = new Pipe(_pipeOptions);
        var fillTask = FillPipeAsync(contentStream, pipe.Writer);
        var readTask = ReadPipeAsync(pipe.Reader, stream, progress);
        
        await Task.WhenAll(fillTask, readTask);
    }

    private async Task FillPipeAsync(Stream source, PipeWriter writer)
    { 
        int bufferSize = Math.Max(1024*512, _config.BufferSize);

        while (true)
        {
            Memory<byte> memory = writer.GetMemory(bufferSize);
            int bytesRead = await source.ReadAsync(memory);

            if (bytesRead == 0)
            {
                break;
            }
            
            writer.Advance(bytesRead);
            
            FlushResult flushResult = await writer.FlushAsync();
            if (flushResult.IsCompleted)
            {
                break;
            }
        }
    }

    private async Task ReadPipeAsync(PipeReader reader, Stream destination, IProgress<long> progress)
    {
        long totalBytesWritten = 0;

        while (true)
        {
            ReadResult result = await reader.ReadAsync();
            ReadOnlySequence<byte> buffer = result.Buffer;

            foreach (var segment in buffer)
            {
                await destination.WriteAsync(segment);
                totalBytesWritten += segment.Length;
            }
            
            progress?.Report(totalBytesWritten);
            reader.AdvanceTo(buffer.End);

            if (result.IsCompleted)
            {
                break;
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

        var message = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
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
            if (_config.ShowProgress && totalWritten % ((piece.Item2-piece.Item1) / _config.BufferSize) == 0)
            {
                _pBar?.Tick();
            }
        });
        await using var stream = _mmf.CreateViewStream();
        await CopyMessageContentToStreamWithProgressAsync(message, stream, progress);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _mmf?.Dispose();
        _pBar?.Dispose();
    }
}