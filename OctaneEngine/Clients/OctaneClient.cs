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
using System.IO.MemoryMappedFiles;
using System.IO.Pipelines;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OctaneEngineCore.Implementations.NetworkAnalyzer;
using OctaneEngineCore.ShellProgressBar;
using OctaneEngineCore.Streams;

// ReSharper disable TemplateIsNotCompileTimeConstantProblem

namespace OctaneEngineCore.Clients;

public class OctaneClient : IClient
{
    private readonly HttpClient _client;
    private readonly OctaneConfiguration _config;
    private readonly ILogger<IClient> _log;
    private readonly ILoggerFactory _loggerFactory;
    private ArrayPool<byte> _memPool;
    private MemoryMappedFile _mmf;
    private ProgressBar _progressBar;
    private readonly PipeOptions _pipeOptions;

    public OctaneClient(OctaneConfiguration config, HttpClient httpClient, ILoggerFactory loggerFactory, ProgressBar progressBar = null)
    {
        _config = config;
        _pipeOptions = new PipeOptions(
            pool: null, // use default
            readerScheduler: PipeScheduler.ThreadPool,
            writerScheduler: PipeScheduler.ThreadPool,
            pauseWriterThreshold: _config.BufferSize * 4,
            resumeWriterThreshold: _config.BufferSize,
            minimumSegmentSize: _config.BufferSize,
            useSynchronizationContext: false
        );
        _client = httpClient;
        _loggerFactory = loggerFactory;
        _progressBar = progressBar;
        _log = loggerFactory.CreateLogger<IClient>();
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
    
    public async Task Download(string url,(long, long) piece, Dictionary<string, string> headers, CancellationToken cancellationToken, PauseToken pauseToken)
    {
        _log.LogTrace("Sending request for range ({PieceItem1},{PieceItem2})...", piece.Item1, piece.Item2);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
        request.Headers.Range = new RangeHeaderValue(piece.Item1, piece.Item2);
        if (headers != null)
        {
            foreach (var header in headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }
        }

        var message = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        
        #region Variable Declaration
        var stopwatch = new Stopwatch();
        var programBps = _config.BytesPerSecond / _config.Parts;
        long bytesReadOverall = 0;
        var childOptions = new ProgressBarOptions
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
            _log.LogDebug("HTTP request returned success status code {code} for piece ({PieceItem1:N0}, {PieceItem2:N0})", (int)message.StatusCode , piece.Item1, piece.Item2);
            await using (var networkStream = await message.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            {
                IStream wrappedStream = _config.BytesPerSecond <= 1 ? new NormalStream() : new ThrottleStream(_loggerFactory);
                wrappedStream.SetStreamParent(networkStream);
                wrappedStream.SetBps(programBps);

                // Only create child progress bar if ShowProgress is enabled and we have a progress bar
                using var child = (_config.ShowProgress && _progressBar != null) 
                    ? _progressBar.Spawn(Convert.ToInt32(piece.Item2 - piece.Item1), "Downloading part...", childOptions)
                    : null;

                if (_config.LowMemoryMode)
                {
                    if (_config.BytesPerSecond > 1)
                    {
                        throw new ArgumentException("Low memory mode cannot be used while bytes per second is enabled (greater than 1).");
                    }
                    
                    try
                    {
                        var pipe = new Pipe(_pipeOptions);
                        var writing = FillPipeAsync(wrappedStream, pipe.Writer, cancellationToken);
                        var reading = ReadPipeToFileAsync(pipe.Reader, piece, child, cancellationToken);
                        await Task.WhenAll(reading, writing);
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "Error: {ex}", ex);
                        throw;
                    }
                }
                else
                {
                    var readBuffer = _memPool.Rent(_config.BufferSize);
                    _log.LogDebug("Buffer rented of size {ConfigBufferSize} for piece ({PieceItem1},{PieceItem2})", _config.BufferSize, piece.Item1, piece.Item2);
                    var stream = _mmf.CreateViewStream(piece.Item1, 0);
                    try
                    {
                        while (true)
                        {
                            var bytesRead = await wrappedStream.ReadAsync(readBuffer.AsMemory(), cancellationToken);
                            if (bytesRead == 0)
                            {
                                break;
                            }
                            await stream.WriteAsync(readBuffer.AsMemory().Slice(0, bytesRead), cancellationToken);
                            bytesReadOverall += bytesRead;
                            child?.Tick((int)bytesReadOverall);
                        }
                    }
                    finally
                    {
                        // Flush and dispose of the MemoryMappedViewStream
                        await stream.FlushAsync(cancellationToken);
                        await stream.DisposeAsync();
                        await wrappedStream.DisposeAsync();
                    }
                    _memPool.Return(readBuffer);
                    _log.LogInformation("Buffer returned to memory pool");
                }

            }
        }
        else
        {
            _log.LogError("HTTP request returned success status code {code} for piece ({PieceItem1},{PieceItem2})", (int)message.StatusCode , NetworkAnalyzer.PrettySize(piece.Item1), NetworkAnalyzer.PrettySize(piece.Item2));
        }
        
        // Only tick the progress bar if ShowProgress is enabled
        if (_config.ShowProgress)
        {
            _progressBar?.Tick();
        }
        stopwatch.Stop();
        _log.LogInformation("Piece ({PieceItem1},{PieceItem2}) finished in {StopwatchElapsedMilliseconds:N0}ms.", NetworkAnalyzer.PrettySize(piece.Item1), NetworkAnalyzer.PrettySize(piece.Item2), stopwatch.ElapsedMilliseconds);
    }

    private async Task FillPipeAsync(IStream stream, PipeWriter writer, CancellationToken token)
    {
        while (true)
        {
            var memory = writer.GetMemory(_config.BufferSize);
            int bytesRead = await stream.ReadAsync(memory, token);

            if (bytesRead == 0)
                break; // End of stream

            writer.Advance(bytesRead);

            var result = await writer.FlushAsync(token);
            if (result.IsCompleted || result.IsCanceled)
                break; // The reader is done or cancelled
        }
    }

    private (int bytesWritten, bool done) Write(ReadOnlyMemory<byte> segment, long accessorLength, nint accessorPtr, long writeOffset)
    {
        var span = segment.Span;
        int bytesToWrite = span.Length;
        
        long remaining = accessorLength - writeOffset;
        if (remaining <= 0)
        {
            return (0, false);
        }
        
        int safeBytesToWrite = (int)Math.Min(bytesToWrite, remaining);
        
        WriteToAccessor(accessorPtr, accessorLength, span[..safeBytesToWrite], writeOffset);
        
        return (safeBytesToWrite, false);
    }
    
    private async Task ReadPipeToFileAsync(PipeReader reader, (long, long) piece, ChildProgressBar child, CancellationToken token)
    {
        long accessorLength = piece.Item2 - piece.Item1 + 1;
        using var accessor = _mmf.CreateViewAccessor(piece.Item1, accessorLength);
        IntPtr accessorPtr = IntPtr.Zero;
        long writeOffset = 0;
        
        try
        {
            unsafe
            {
                byte* ptr = null;
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
                byte* accessorBasePtr = ptr + accessor.PointerOffset;
                accessorPtr = (IntPtr)accessorBasePtr;
            }
            
            int tickStep = _config.BufferSize * 2;
            int lastTick = 0;

            while (true)
            {
                ReadResult result = await reader.ReadAsync(token);
                ReadOnlySequence<byte> buffer = result.Buffer;

                foreach (var segment in buffer)
                {
                    (int safeBytesToWrite, bool isDone) = Write(segment, accessorLength, accessorPtr, writeOffset);
                    
                    if (isDone)
                    {
                        break;
                    }
                    
                    writeOffset += safeBytesToWrite;

                    if (writeOffset - lastTick >= tickStep || writeOffset == accessorLength)
                    {
                        child?.Tick((int)writeOffset);
                        lastTick = (int)writeOffset;
                    }

                    if (writeOffset >= accessorLength)
                    {
                        break;
                    }
                }
                    
                reader.AdvanceTo(buffer.End);

                if (result.IsCompleted || result.IsCanceled || writeOffset >= accessorLength)
                {
                    break;
                }
            }

            accessor.Flush();
        }
        catch (AccessViolationException ex)
        {
            _log.LogError(ex, "Memory access error while writing to accessor with offset {offset}.", writeOffset);
        }
        catch (ArgumentException ex)
        {
            _log.LogError(ex, "Invalid argument error while writing to accessor with offset {offset}.", writeOffset);
        }
        catch (InvalidOperationException ex)
        {
            _log.LogError(ex, "Invalid operation error while writing to accessor with offset {offset}.", writeOffset);
        }
        finally{
            if (accessorPtr != IntPtr.Zero){
                accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            }
        }
    }
    
    private unsafe void WriteToAccessor(IntPtr basePtr, long accessorLength, ReadOnlySpan<byte> buffer, long offset)
    {
        long remaining = accessorLength - offset;
        int safeBytesToWrite = (int)Math.Min(buffer.Length, remaining);

        if (safeBytesToWrite <= 0)
        {
            return;
        }

        byte* dest = (byte*)basePtr + offset;

        fixed (byte* src = buffer)
        {
            Unsafe.CopyBlockUnaligned(dest, src, (uint)safeBytesToWrite);
        }
    }
}