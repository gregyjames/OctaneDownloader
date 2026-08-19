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
using System.IO.Pipelines;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OctaneEngineCore.Implementations.NetworkAnalyzer;
using OctaneEngineCore.ShellProgressBar;
using OctaneEngineCore.Streams;

namespace OctaneEngineCore.Clients;

public partial class OctaneClient : IClient
{
    private readonly HttpClient _client;
    private readonly OctaneConfiguration _config;
    private readonly ILogger<IClient> _log;
    private readonly ILoggerFactory _loggerFactory;
    private ArrayPool<byte> _memPool = ArrayPool<byte>.Shared;
    private IFileWriter? _writer;
    private ProgressBar? _progressBar;
    private readonly PipeOptions _pipeOptions;
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
        _pipeOptions = new PipeOptions(
            pool: null, // use default
            readerScheduler: PipeScheduler.ThreadPool,
            writerScheduler: PipeScheduler.ThreadPool,
            pauseWriterThreshold: Math.Max(_config.BufferSize * 16, 16 * 1024 * 1024),  // 16 MB min
            resumeWriterThreshold: Math.Max(_config.BufferSize * 8, 8 * 1024 * 1024), // 8 MB min
            minimumSegmentSize: Math.Max(_config.BufferSize, 512 * 1024),              // 512 KB min
            useSynchronizationContext: false
        );
        _client = httpClient;
        _loggerFactory = loggerFactory;
        _progressBar = progressBar;
        _log = loggerFactory.CreateLogger<IClient>();
        _tickStep = Math.Max(_config.BufferSize * 2L, 1 * 1024 * 1024L);
    }
    
    public bool IsRangeSupported() => true;

    public void SetWriter(IFileWriter writer) => _writer = writer;
    public void SetProgressbar(ProgressBar bar) => _progressBar = bar;
    private async ValueTask<HttpResponseMessage> SendRangeRequestAsync(
        Uri uri, 
        (long start, long end) piece, 
        Dictionary<string, string>? headers, 
        CancellationToken cancellationToken)
    {
        HttpRequestMessage request = new()
        {
            Method = HttpMethod.Get,
            #if NET6_0_OR_GREATER
                VersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
                Version = System.Net.HttpVersion.Version30,
            #else
                Version = Polyfills.HttpVersion20,
            #endif
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
    
    public async Task Download(string url,(long start, long end) piece, Dictionary<string, string>? headers, CancellationToken cancellationToken, PauseToken pauseToken)
    {
        await pauseToken.WaitWhilePausedAsync(cancellationToken).ConfigureAwait(false);
        LogSendingRequestForRangePieces(piece.start, piece.end);
        var stopwatch = new Stopwatch();
        var uri = new Uri(url, UriKind.Absolute);
        using var message = await SendRangeRequestAsync(uri, piece, headers, cancellationToken).ConfigureAwait(false);
        
        #region Variable Declaration
        var programBps = _config.BytesPerSecond / _config.Parts;
        #endregion
        
        stopwatch.Start();
        if (!message.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Download failed with status code: {message.StatusCode}");
        }
        if (message.StatusCode != System.Net.HttpStatusCode.PartialContent)
        {
            throw new InvalidOperationException($"Expected HTTP 206 Partial Content, but received {(int)message.StatusCode}.");
        }
            
        var contentRange = message.Content.Headers.ContentRange;
        if (contentRange == null || contentRange.From != piece.start || contentRange.To != piece.end)
        {
            throw new InvalidOperationException("Invalid or missing Content-Range in response.");
        }

        LogHttpRequestReturnedSuccessStatus((int)message.StatusCode, piece.start, piece.end);
        using var networkStream = await message.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var wrappedStream = _config.BytesPerSecond <= 1 ? networkStream : new ThrottleStream(_loggerFactory);

        if (wrappedStream is ThrottleStream throttleStream)
        {
            throttleStream.SetStreamParent(networkStream);
            throttleStream.SetBps(programBps);
        }

        // Only create child progress bar if ShowProgress is enabled, and we have a progress bar
        using var child = (_config.ShowProgress && _progressBar != null) 
            ? _progressBar.Spawn(piece.end - piece.start, "Downloading part...", ChildProgressBarOptions)
            : null;

        if (_writer == null)
        {
            throw new InvalidOperationException("Writer not initialized before download.");
        }

        await using var chunkWriter = _writer.CreateChunkWriter(piece.start, piece.end - piece.start + 1);

        if (_config.LowMemoryMode && chunkWriter is IMemoryMappedChunkWriter mmChunkWriter)
        {
            await LowMemoryDownload(piece, cancellationToken, wrappedStream, child, pauseToken, mmChunkWriter).ConfigureAwait(false);
        }
        else
        {
            await RegularDownload(piece, cancellationToken, wrappedStream, child, pauseToken, chunkWriter).ConfigureAwait(false);
        }
        
        // Only tick the progress bar if ShowProgress is enabled
        if (_config.ShowProgress)
        {
            _progressBar?.Tick();
        }
        stopwatch.Stop();
        LogPieceExecutionTimeElapsedMilliseconds(NetworkAnalyzer.PrettySize(piece.start), NetworkAnalyzer.PrettySize(piece.end), stopwatch.ElapsedMilliseconds);
    }

    private async Task RegularDownload((long start, long end) piece, CancellationToken cancellationToken, Stream wrappedStream, ChildProgressBar? child, PauseToken pauseToken, IChunkWriter chunkWriter)
    {
        int readBufferSize = Math.Max(_config.BufferSize, 256 * 1024);
        var readBuffer = _memPool.Rent(readBufferSize);
        
        if(_log.IsEnabled(LogLevel.Debug))
        {
            LogBufferRentedOfSize(_config.BufferSize, piece.start, piece.end);
        }
                
        try
        {
            long expectedLength = piece.end - piece.start + 1;
            await CopyStreamToWriterAsync(wrappedStream, chunkWriter, readBuffer, expectedLength, child, pauseToken, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await wrappedStream.DisposeAsync().ConfigureAwait(false);
            _memPool.Return(readBuffer);
            LogBufferReturnedToMemoryPool();
        }
    }

    private async Task CopyStreamToWriterAsync(Stream source, IChunkWriter destination, byte[] buffer, long expectedLength, ChildProgressBar? child, PauseToken pauseToken, CancellationToken cancellationToken)
    {
        long bytesReadOverall = 0;
        int progressUpdateInterval = buffer.Length * 4;
        long lastProgressUpdate = 0L;

        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            await pauseToken.WaitWhilePausedAsync(cancellationToken).ConfigureAwait(false);
            
            if (bytesRead == 0)
            {
                break;
            }

            if (bytesReadOverall + bytesRead > expectedLength)
            {
                throw new InvalidOperationException("Received data exceeds the requested piece size.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            
            bytesReadOverall += bytesRead;
                    
            if (child != null && (bytesReadOverall - lastProgressUpdate >= progressUpdateInterval))
            {
                child.Tick(bytesReadOverall);
                lastProgressUpdate = bytesReadOverall;
            }
        }

        if (bytesReadOverall < expectedLength)
        {
            throw new InvalidOperationException("Response stream ended before the requested piece was fully downloaded.");
        }
    }

    private async Task LowMemoryDownload((long start, long end) piece, CancellationToken cancellationToken, Stream wrappedStream, ChildProgressBar? child, PauseToken pauseToken, IMemoryMappedChunkWriter chunkWriter)
    {
        if (_config.BytesPerSecond > 1)
        {
            throw new ArgumentException("Low memory mode cannot be used while bytes per second is enabled (greater than 1).");
        }

        using var accessor = chunkWriter.CreateViewAccessor();
        IntPtr accessorPtr = IntPtr.Zero;

        try
        {
            unsafe
            {
                byte* ptr = null;
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
                byte* accessorBasePtr = ptr + accessor.PointerOffset;
                accessorPtr = (IntPtr)accessorBasePtr;
            }

            try
            {
                var pipe = new Pipe(_pipeOptions);
                var writing = FillPipeAsync(wrappedStream, pipe.Writer, cancellationToken, pauseToken);
                var reading = ReadPipeToFileAsync(pipe.Reader, piece, child, accessorPtr, cancellationToken, pauseToken);
                await Task.WhenAll(reading, writing).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogError(ex);
                throw;
            }
        }
        finally{
            if (accessorPtr != IntPtr.Zero){
                accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            }

            await wrappedStream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task FillPipeAsync(Stream stream, PipeWriter writer, CancellationToken token, PauseToken pauseToken)
    {
        int bufferSize = Math.Max(_config.BufferSize, 512 * 1024);

        try
        {
            while (true)
            {
                var memory = writer.GetMemory(bufferSize);
                var bytesRead = await stream.ReadAsync(memory, token).ConfigureAwait(false);
                await pauseToken.WaitWhilePausedAsync(token).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                } // End of stream 

                writer.Advance(bytesRead);
                var result = await writer.FlushAsync(token).ConfigureAwait(false);
                if (result.IsCompleted || result.IsCanceled)
                {
                    break;
                } // The reader is done or cancelled
            }
        }
        finally
        {
            await writer.CompleteAsync().ConfigureAwait(false);
        }
    }
    
    private async Task ReadPipeToFileAsync(PipeReader reader, (long start, long end) piece, ChildProgressBar? child, IntPtr accessorPtr, CancellationToken token, PauseToken pauseToken)
    {
        long accessorLength = piece.end - piece.start + 1;
        long writeOffset = 0;

        try
        {
            long lastTick = 0;

            while (true)
            {
                if (!reader.TryRead(out ReadResult result))
                    result = await reader.ReadAsync(token).ConfigureAwait(false);

                await pauseToken.WaitWhilePausedAsync(token).ConfigureAwait(false);

                ReadOnlySequence<byte> buffer = result.Buffer;

                if (buffer.IsSingleSegment)
                {
                    writeOffset = WriteSpan(buffer.First.Span, accessorPtr, writeOffset, accessorLength, child, ref lastTick);
                }
                else
                {
                    foreach (var segment in buffer)
                    {
                        writeOffset = WriteSpan(segment.Span, accessorPtr, writeOffset, accessorLength, child, ref lastTick);
                        if (writeOffset >= accessorLength)
                        {
                            break;
                        }
                    }
                }

                reader.AdvanceTo(buffer.End);

                if (result.IsCompleted || result.IsCanceled || writeOffset >= accessorLength)
                {
                    break;
                }
            }
        }
        catch (AccessViolationException ex)
        {
            LogMemoryAccessErrorWhileWritingToAccessorWithOffsetOffset(ex, writeOffset);
            throw;
        }
        catch (ArgumentException ex)
        {
            LogInvalidArgumentErrorWhileWritingToAccessorWithOffsetOffset(ex, writeOffset);
        }
        catch (InvalidOperationException ex)
        {
            LogInvalidOperationErrorWhileWritingToAccessorWithOffsetOffset(ex, writeOffset);
        }
        finally
        {
            await reader.CompleteAsync().ConfigureAwait(false);
        }
    }


    private unsafe long WriteSpan(ReadOnlySpan<byte> span, IntPtr accessorPtr, long writeOffset, long accessorLength, ChildProgressBar? child, ref long lastTick)
    {
        long remaining = accessorLength - writeOffset;
        if (remaining <= 0) return writeOffset;

        int safeBytesToWrite = (int)Math.Min(span.Length, remaining);

        byte* dest = (byte*)accessorPtr + writeOffset;
        byte* src = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(span));
        Unsafe.CopyBlockUnaligned(dest, src, (uint)safeBytesToWrite);

        writeOffset += safeBytesToWrite;

        if (writeOffset - lastTick >= _tickStep || writeOffset == accessorLength)
        {
            child?.Tick((int)Math.Min(writeOffset, int.MaxValue));
            lastTick = writeOffset;
        }
        
        return writeOffset;
    }

    [LoggerMessage(LogLevel.Trace, "Sending request for range ({pieceItem1},{pieceItem2})...")]
    partial void LogSendingRequestForRangePieces(long pieceItem1, long pieceItem2);

    [LoggerMessage(LogLevel.Debug, "HTTP request returned success status code {code} for piece ({pieceItem1:N0}, {pieceItem2:N0})")]
    partial void LogHttpRequestReturnedSuccessStatus(int code, long pieceItem1, long pieceItem2);

    [LoggerMessage(LogLevel.Error, "Error")]
    partial void LogError(Exception exception);

    [LoggerMessage(LogLevel.Debug, "Buffer rented of size {configBufferSize} for piece ({pieceItem1},{pieceItem2})")]
    partial void LogBufferRentedOfSize(int configBufferSize, long pieceItem1, long pieceItem2);

    [LoggerMessage(LogLevel.Information, "Buffer returned to memory pool")]
    partial void LogBufferReturnedToMemoryPool();

    [LoggerMessage(LogLevel.Error, "HTTP request returned success status code {code} for piece ({pieceItem1},{pieceItem2})")]
    partial void LogHttpRequestReturnedSuccessStatusCodeCode(int code, string pieceItem1, string pieceItem2);

    [LoggerMessage(LogLevel.Information, "Piece ({pieceItem1},{pieceItem2}) finished in {stopwatchElapsedMilliseconds:N0}ms.")]
    partial void LogPieceExecutionTimeElapsedMilliseconds(string pieceItem1, string pieceItem2, long stopwatchElapsedMilliseconds);

    [LoggerMessage(LogLevel.Error, "Memory access error while writing to accessor with offset {offset}.")]
    partial void LogMemoryAccessErrorWhileWritingToAccessorWithOffsetOffset(Exception ex, long offset);

    [LoggerMessage(LogLevel.Error, "Invalid argument error while writing to accessor with offset {offset}.")]
    partial void LogInvalidArgumentErrorWhileWritingToAccessorWithOffsetOffset(Exception ex, long offset);

    [LoggerMessage(LogLevel.Error, "Invalid operation error while writing to accessor with offset {offset}.")]
    partial void LogInvalidOperationErrorWhileWritingToAccessorWithOffsetOffset(Exception ex, long offset);
}