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
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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
    
    const string DllName = "core"; // Adjust the name according to your platform

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int download_partial_file(
        string url,
        ulong start,
        ulong end,
        IntPtr buffer,
        ulong buffer_len);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void free_buffer(IntPtr buffer, ulong buffer_len);

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

    private static async IAsyncEnumerable<int> ProcessStreamAsync(IStream stream1, MemoryMappedViewStream stream2, byte[] readbuffer, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var memory = readbuffer.AsMemory();
        while (true)
        {
            var bytesRead = await stream1.ReadAsync(memory, cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                break;
            }
            await stream2.WriteAsync(readbuffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            yield return bytesRead;
        }
    }

    /*
    private static async IAsyncEnumerable<long> ProcessStreamLowMemoryAsync(IStream stream1, MemoryMappedFile _mmf,
        byte[] readbuffer, (long, long) piece, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        long bytesReadOverall = 0;
        var memory = readbuffer.AsMemory();
        
        while (true)
        {
            /*
            var bytesRead = await stream1.ReadAsync(memory, cancellationToken).ConfigureAwait(false);

            if (bytesRead == 0)
            {
                break;
            }

            using var accessor = _mmf.CreateViewAccessor(piece.Item1 + bytesReadOverall, bytesRead, MemoryMappedFileAccess.Write);
            accessor.WriteArray(0, readbuffer, 0, bytesRead);
            bytesReadOverall += bytesRead;
            yield return bytesReadOverall;
            
            
            IntPtr buffer;
            ulong buffer_len;

            //int result = download_partial_file(_url, (ulong)piece.Item1, (ulong)piece.Item2, buffer, out buffer_len);
            var result = 0;
            using var accessor = _mmf.CreateViewAccessor(piece.Item1 + bytesReadOverall, (long)buffer_len, MemoryMappedFileAccess.Write);
            
            if (result != 0)
            {
                Console.WriteLine("Failed to download the file.");
                break;
            }
            
            byte[] data = new byte[buffer_len];
            Marshal.Copy(buffer, data, 0, (int)buffer_len);
            accessor.WriteArray(0, data, 0, (int)buffer_len);
            free_buffer(buffer, buffer_len);
            bytesReadOverall += (long)buffer_len;
            yield return bytesReadOverall;
        }
    }
    */
    private static string _url = "";
    public async PooledTask Download(string url,(long, long) piece, CancellationToken cancellationToken, PauseToken pauseToken)
    {
        _url = url;
        _log.LogTrace("Sending request for range ({PieceItem1},{PieceItem2})...", piece.Item1, piece.Item2);
        //using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
        //request.Headers.Range = new RangeHeaderValue(piece.Item1, piece.Item2);
        //var message = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
        //    .ConfigureAwait(false);
        
        #region Variable Declaration
        //var readbuffer = _memPool.Rent(_config.BufferSize);
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
        if (true)
        {
            _log.LogInformation("HTTP request returned success status code for piece ({PieceItem1},{PieceItem2})", piece.Item1, piece.Item2);
            
            /*
            IntPtr buffer;
            ulong buffer_len;

            int result = download_partial_file(_url, (ulong)piece.Item1, piece.Item2, out buffer, out buffer_len);
            using var accessor = _mmf.CreateViewAccessor(piece.Item1 + bytesReadOverall, (long)buffer_len, MemoryMappedFileAccess.Write);
            
            Marshal.Copy(buffer, readbuffer, 0, (int)buffer_len);
            accessor.WriteArray(0, readbuffer, 0, (int)buffer_len);
            free_buffer(buffer, buffer_len);
            */
            
            /*
        var readbuffer = _memPool.Rent((int)(piece.Item2 - piece.Item1) + 1);
        GCHandle handle = GCHandle.Alloc(readbuffer, GCHandleType.Pinned);
        IntPtr buffer = handle.AddrOfPinnedObject();
            
        try
        {
            int result = download_partial_file(url, (ulong)piece.Item1, (ulong)piece.Item2, buffer,
                (ulong)readbuffer.Length);
            using var accessor = _mmf.CreateViewAccessor(piece.Item1, (long)result, MemoryMappedFileAccess.Write);
            accessor.WriteArray(0, readbuffer, 0, (int)result);
        }
        finally
        {
            handle.Free();
            _memPool.Return(readbuffer);
        }
        */

            await Task.Run(() =>
            {
                UnsafeWrite(piece, url);
            }, cancellationToken);
        }
        
        _progressBar?.Tick();
        _log.LogInformation("Piece ({PieceItem1},{PieceItem2}) done", piece.Item1, piece.Item2);
        stopwatch.Stop();
        _log.LogInformation("Piece ({PieceItem1},{PieceItem2}) finished in {StopwatchElapsedMilliseconds} ms", piece.Item1, piece.Item2, stopwatch.ElapsedMilliseconds);
    }

    void UnsafeWrite((long, long) piece, string url)
    {
        var readbuffer = _memPool.Rent((int)(piece.Item2 - piece.Item1) + 1);
        try
        {
            unsafe
            {
                fixed (byte* pBuffer = readbuffer)
                {
                    IntPtr buffer = (IntPtr)pBuffer;
                    int result = download_partial_file(url, (ulong)piece.Item1, (ulong)piece.Item2, buffer,
                        (ulong)readbuffer.Length);
                    using var accessor = _mmf.CreateViewAccessor(piece.Item1, (long)result, MemoryMappedFileAccess.Write);
                    accessor.WriteArray(0, readbuffer, 0, (int)result);
                }
            }
        }
        finally
        {
            _memPool.Return(readbuffer, true);
        }
    }
    public void Dispose()
    {
        //_client?.Dispose();
        //_loggerFactory?.Dispose();
        //_mmf?.Dispose();
        //_progressBar?.Dispose();
    }
}