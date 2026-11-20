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

#nullable enable
using System;
using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Win32.SafeHandles;
using OctaneEngineCore.Clients;
using OctaneEngineCore.Implementations.NetworkAnalyzer;
using OctaneEngineCore.Interfaces;
using OctaneEngineCore.ShellProgressBar;
// ReSharper disable All

namespace OctaneEngineCore.Implementations;

public partial class Engine: IEngine, IDisposable
{
    private readonly ILoggerFactory _factory;
    private readonly OctaneClient? _client;
    private readonly DefaultClient? _defaultClient;
    private OctaneConfiguration _config;
    private readonly ILogger<Engine> _logger;
    private readonly IHttpClientFactory _clientFactory;
    public Engine(IOptions<OctaneConfiguration> config, IHttpClientFactory clientFactory, ILoggerFactory factory)
    {
        _clientFactory = clientFactory;
        _factory = factory ?? NullLoggerFactory.Instance;
        _logger = _factory.CreateLogger<Engine>();
        _config = config.Value;
    }

    /// <summary>
    /// Creates a new Engine instance without dependency injection
    /// </summary>
    internal Engine(OctaneClient? client, DefaultClient? defaultClient, IHttpClientFactory clientFactory,
        OctaneConfiguration config, ILoggerFactory? factory = null)
    {
        _factory = factory ?? NullLoggerFactory.Instance;
        _logger = _factory.CreateLogger<Engine>();
        _client = client;
        _defaultClient = defaultClient;
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _clientFactory = clientFactory;
    }

    /// <summary>
    /// Creates a new Engine instance with a pre-configured HttpClient, useful for testing.
    /// </summary>
    public Engine(OctaneConfiguration config, HttpClient httpClient, ILoggerFactory? factory = null)
    {
        _factory = factory ?? NullLoggerFactory.Instance;
        _logger = _factory.CreateLogger<Engine>();
        _config = config ?? throw new ArgumentNullException(nameof(config));
        
        // Normalize config values just like EngineBuilder does
        if (_config.Parts <= 0)
            _config.Parts = OctaneConfiguration.DefaultParts;
        if (_config.BufferSize <= 0)
            _config.BufferSize = 8192;
        if (_config.NumRetries < 0)
            _config.NumRetries = 3;
        if (_config.BytesPerSecond <= 0)
            _config.BytesPerSecond = 1;

        _clientFactory = new SingleHttpClientFactory(httpClient);
        _client = new OctaneClient(_config, httpClient, _factory, null);
        _defaultClient = new DefaultClient(httpClient, _config);
    }
        
    #region Helpers
    private void Cleanup(Stopwatch stopwatch, OctaneConfiguration config, bool success)
    {
        LogCallingCallbackFunction();
        if (!success)
        {
            LogDownloadFailed();
        }
        config.DoneCallback?.Invoke(success);
    }
    #endregion

    /// <summary>
    /// Gets the optimal number of parts to use to download a file by downloading a small test file (1MB) and testing network latency.
    /// </summary>
    /// <param name="url">The url of the file you are planning to download.</param>
    /// <param name="sizeToUse">The size of the test file to use.</param>
    /// <returns>A Task that returns the optimal number of parts to use to download a file.</returns>
    public async static Task<int> GetOptimalNumberOfParts(string url, TestFileSize sizeToUse = TestFileSize.Small)
    {
        if (!Enum.IsDefined(typeof(TestFileSize), sizeToUse))
            throw new InvalidEnumArgumentException(nameof(sizeToUse), (int)sizeToUse,
                typeof(TestFileSize));
        using var client = new HttpClient();
        var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        var size_of_file = response.Content.Headers.ContentLength ?? 0;
        var networkSpeed = await NetworkAnalyzer.NetworkAnalyzer.GetNetworkSpeed(NetworkAnalyzer.NetworkAnalyzer.GetTestFile(sizeToUse), new HttpDownloader());
        var networkLatency = await NetworkAnalyzer.NetworkAnalyzer.GetNetworkLatency(new PingService());
        int chunkSize = (int)Math.Ceiling(Math.Sqrt((double)networkSpeed * networkLatency));
        int numParts = (int)Math.Ceiling((double)size_of_file / chunkSize);
        numParts = Math.Min(numParts, OctaneConfiguration.DefaultParts);
        return numParts;
    }
        
    public async Task<string> GetCurrentNetworkLatency()
    {
        return $"{await NetworkAnalyzer.NetworkAnalyzer.GetNetworkLatency(new PingService())}ms";
    }
    public async Task<string> GetCurrentNetworkSpeed()
    {
        var speed = await NetworkAnalyzer.NetworkAnalyzer.GetNetworkSpeed(NetworkAnalyzer.NetworkAnalyzer.GetTestFile(TestFileSize.Medium), new HttpDownloader());
        return $"{ Convert.ToInt32((speed) / 1000000)} Mb/s";
    }
    
    internal enum ClientType
    {
        Octane,
        Normal
    }
    
    /// <summary>
    /// Sets the progress callback for when the progress bar isn't being used.
    /// </summary>
    /// <param name="callback">The Action that is called when a part is completed. </param>
    public void SetProgressCallback(Action<double> callback)
    {
        _config.ProgressCallback += callback;
    }

    /// <summary>
    /// Sets the download completed action.
    /// </summary>
    /// <param name="callback">The action to be called when the download in done. Returns a bool indicating success.</param>
    public void SetDoneCallback(Action<bool> callback)
    {
        _config.DoneCallback += callback;
    }

    /// <summary>
    /// Sets the proxy server to use when downloading the file.
    /// </summary>
    /// <param name="proxy">The IWebProxy to use.</param>
    public void SetProxy(IWebProxy proxy)
    {
        _config.Proxy = proxy;
    }

    /// <summary>
    ///     The core octane download function. 
    /// </summary>
    /// <param name="request">The OctaneRequest Object for the request.</param>
    /// <param name="pauseTokenSource">The pause token source to use for pausing and resuming.</param>
    /// <param name="token">The cancellation token to use for the download.</param>
    public async Task DownloadFile(OctaneRequest request, PauseTokenSource? pauseTokenSource = null, CancellationToken token = default)
    {
        var stopwatch = new Stopwatch();
        var success = false;
        var filename = string.Empty;
        ClientType clientType = ClientType.Normal;
        HttpClient? client = null;

        try
        {
            client = _clientFactory.CreateClient("OctaneClient");
            
            // Thread-safe local variable resolution to support concurrency
            var octaneClient = _client ?? new OctaneClient(_config, client, _factory);
            var defaultClient = _defaultClient ?? new DefaultClient(client, _config);

            var uri = new Uri(request.Url, UriKind.Absolute);
            filename = request.OutFile ?? Path.GetFileName(uri.LocalPath);
            var cancellation_token = token;
            var pause_token = pauseTokenSource ?? new PauseTokenSource(_factory);
            var headers = request.Headers ?? [];

            LogServerFileNameFilename(filename);

            if (pause_token.Token.IsPaused)
            {
                await pause_token.Token.WaitWhilePausedAsync(cancellation_token).ConfigureAwait(false);
            }

            stopwatch.Start();

            // 1-RTT Fast Start: probe with GET Range: bytes=0-<firstChunkEnd> instead of a separate HEAD round-trip.
            long probeChunkSize = _config.MaxChunkSize > 0
                ? _config.MaxChunkSize
                : (_config.MinPartSize > 0 ? _config.MinPartSize : 8L * 1024 * 1024);

            using var initialResponse = await octaneClient
                .SendRangeRequestAsync(uri, (0, 0), headers, cancellation_token)
                .ConfigureAwait(false);

            if (initialResponse.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                // Empty 0-byte file
                using (File.OpenHandle(filename, FileMode.Create, FileAccess.Write, FileShare.None)) { }
                success = true;
                stopwatch.Stop();
                LogFileDownloadedSuccessfullyInStopwatchelapsedmillisecondsMs(stopwatch.ElapsedMilliseconds);
                return;
            }

            initialResponse.EnsureSuccessStatusCode();

            // Reuse the final redirected URI (e.g. CDN edge URL) so subsequent chunks skip 301/302 redirect round-trips
            var effectiveUri = initialResponse.RequestMessage?.RequestUri ?? uri;

            bool rangeSupported = initialResponse.StatusCode == HttpStatusCode.PartialContent
                                  && initialResponse.Content.Headers.ContentRange?.Length is > 0;
            clientType = rangeSupported ? ClientType.Octane : ClientType.Normal;
            LogRangeSupportedRange(rangeSupported);

            long length = rangeSupported
                ? initialResponse.Content.Headers.ContentRange!.Length!.Value
                : (initialResponse.Content.Headers.ContentLength ?? 0);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                LogTotalSizeLength(NetworkAnalyzer.NetworkAnalyzer.PrettySize(length));
            }

            using (SafeFileHandle fileHandle = File.OpenHandle(
                       filename,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None,
                       FileOptions.Asynchronous | FileOptions.RandomAccess,
                       preallocationSize: length > 0 ? length : 0))
            {
                if (length > 0)
                {
                    RandomAccess.SetLength(fileHandle, length);
                }

                if (clientType == ClientType.Octane)
                {
                    long firstEnd = initialResponse.Content.Headers.ContentRange!.To
                                    ?? Math.Min(probeChunkSize - 1, length - 1);
                    long remainingOffset = firstEnd + 1;
                    long remainingLength = length - remainingOffset;

                    var remainingPieces = remainingLength > 0
                        ? Helpers.CreatePartsList(remainingLength, _config.Parts, _logger, _config.MinPartSize, _config.MaxChunkSize, startOffset: remainingOffset)
                        : [];

                    int totalChunks = 1 + remainingPieces.Count;
                    int tasksDone = 0;

                    LogUsingOctaneClientToDownloadFile();
                    ProgressBar? pbar = null;

                    if (_config.ShowProgress)
                    {
                        pbar = new ProgressBar(totalChunks * 2, "Downloading file...");
                        octaneClient.SetProgressbar(pbar);
                        defaultClient.SetProgressbar(pbar);
                    }

                    var inFlightChunks = new InFlightChunk?[totalChunks];
                    bool hedgeEnabled = _config.EnableTailHedging && _config.TailHedgeDelayMs >= 0 && _config.Parts > 1 && totalChunks > 1;
                    long completedBytesTotal = 0;
                    long completedDurationTicksTotal = 0;

                    try
                    {
                        int workerCount = Math.Max(1, Math.Min(_config.Parts, totalChunks));
                        int nextPieceIndex = -1;

                        void CompleteChunk(InFlightChunk chunk)
                        {
                            if (chunk.TryMarkCompleted())
                            {
                                long elapsedTicks = Math.Max(1, Stopwatch.GetTimestamp() - chunk.StartTimestamp);
                                Interlocked.Add(ref completedBytesTotal, chunk.TotalBytes);
                                Interlocked.Add(ref completedDurationTicksTotal, elapsedTicks);

                                int done = Interlocked.Increment(ref tasksDone);
                                pbar?.Tick();
                                _config?.ProgressCallback?.Invoke((double)done / totalChunks);
                            }
                        }

                        static async Task CleanupPrefetchAsync(InFlightChunk? prefetchedChunk, Task<HttpResponseMessage>? prefetchedResponseTask)
                        {
                            if (prefetchedChunk != null)
                            {
                                try { prefetchedChunk.Cts.Cancel(); } catch (ObjectDisposedException) { }
                            }

                            if (prefetchedResponseTask != null)
                            {
                                try
                                {
                                    var resp = await prefetchedResponseTask.ConfigureAwait(false);
                                    resp.Dispose();
                                }
                                catch
                                {
                                    // Ignore errors/cancellations during cleanup
                                }
                            }

                            prefetchedChunk?.Dispose();
                        }

                        async Task RunWorkerAsync(bool handleInitialChunk)
                        {
                                InFlightChunk? prefetchedChunk = null;
                                Task<HttpResponseMessage>? prefetchedResponseTask = null;

                                async Task WriteChunkWithPrefetchAsync(HttpResponseMessage response, InFlightChunk currentChunk)
                                {
                                    var writeTask = octaneClient.WriteResponseAsync(
                                        response,
                                        currentChunk.Piece,
                                        fileHandle,
                                        currentChunk.Cts.Token,
                                        pause_token.Token,
                                        currentChunk);

                                    long halfBytes = currentChunk.TotalBytes / 2;
                                    if (halfBytes >= 128 * 1024 && Volatile.Read(ref nextPieceIndex) < remainingPieces.Count - 1)
                                    {
                                        using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(currentChunk.Cts.Token);
                                        while (!writeTask.IsCompleted)
                                        {
                                            if (!pause_token.Token.IsPaused && currentChunk.BytesCommitted >= halfBytes)
                                            {
                                                int prefetchIdx = Interlocked.Increment(ref nextPieceIndex);
                                                if (prefetchIdx < remainingPieces.Count)
                                                {
                                                    prefetchedChunk = new InFlightChunk(remainingPieces[prefetchIdx], cancellation_token);
                                                    Volatile.Write(ref inFlightChunks[prefetchIdx + 1], prefetchedChunk);
                                                    prefetchedResponseTask = octaneClient.SendRangeRequestAsync(
                                                        effectiveUri,
                                                        prefetchedChunk.Piece,
                                                        headers,
                                                        prefetchedChunk.Cts.Token).AsTask();
                                                }
                                                break;
                                            }

                                            var delayTask = Task.Delay(25, delayCts.Token);
                                            var completed = await Task.WhenAny(writeTask, delayTask).ConfigureAwait(false);
                                            if (completed == writeTask)
                                            {
                                                delayCts.Cancel();
                                                break;
                                            }
                                        }
                                    }

                                    await writeTask.ConfigureAwait(false);
                                }

                                try
                                {
                                    if (handleInitialChunk)
                                    {
                                        var initialChunk = new InFlightChunk((0, firstEnd), cancellation_token);
                                        Volatile.Write(ref inFlightChunks[0], initialChunk);
                                        try
                                        {
                                            await WriteChunkWithPrefetchAsync(initialResponse, initialChunk).ConfigureAwait(false);
                                            CompleteChunk(initialChunk);
                                        }
                                        catch (OperationCanceledException) when (initialChunk.IsCompleted && !cancellation_token.IsCancellationRequested)
                                        {
                                            // Hedged sibling already completed this chunk
                                        }
                                    }

                                    while (true)
                                    {
                                        cancellation_token.ThrowIfCancellationRequested();

                                        InFlightChunk chunk;
                                        Task<HttpResponseMessage>? responseTask = null;

                                        if (prefetchedChunk != null && prefetchedResponseTask != null)
                                        {
                                            chunk = prefetchedChunk;
                                            responseTask = prefetchedResponseTask;
                                            prefetchedChunk = null;
                                            prefetchedResponseTask = null;
                                        }
                                        else
                                        {
                                            int idx = Interlocked.Increment(ref nextPieceIndex);
                                            if (idx >= remainingPieces.Count)
                                            {
                                                break;
                                            }

                                            chunk = new InFlightChunk(remainingPieces[idx], cancellation_token);
                                            Volatile.Write(ref inFlightChunks[idx + 1], chunk);
                                        }

                                        try
                                        {
                                            if (pause_token.Token.IsPaused)
                                            {
                                                await pause_token.Token.WaitWhilePausedAsync(chunk.Cts.Token).ConfigureAwait(false);
                                            }

                                            using var response = responseTask != null
                                                ? await responseTask.ConfigureAwait(false)
                                                : await octaneClient.SendRangeRequestAsync(
                                                    effectiveUri,
                                                    chunk.Piece,
                                                    headers,
                                                    chunk.Cts.Token).ConfigureAwait(false);

                                            await WriteChunkWithPrefetchAsync(response, chunk).ConfigureAwait(false);
                                            CompleteChunk(chunk);
                                        }
                                        catch (OperationCanceledException) when (chunk.IsCompleted && !cancellation_token.IsCancellationRequested)
                                        {
                                            // Hedged sibling already completed this chunk
                                        }
                                    }
                                }
                                finally
                                {
                                    if (prefetchedChunk != null || prefetchedResponseTask != null)
                                    {
                                        await CleanupPrefetchAsync(prefetchedChunk, prefetchedResponseTask).ConfigureAwait(false);
                                        prefetchedChunk = null;
                                        prefetchedResponseTask = null;
                                    }
                                }

                                // Progress-aware tail work stealing: once the queue is drained, steal only the
                                // remaining uncommitted tail bytes of stalled or severely lagging in-flight chunks.
                                if (hedgeEnabled)
                                {
                                    const long minStealBytesForSlowCheck = 64 * 1024;
                                    double slowCheckMinElapsedMs = Math.Min(_config.TailHedgeDelayMs, 500);

                                    while (Volatile.Read(ref tasksDone) < totalChunks && !cancellation_token.IsCancellationRequested)
                                    {
                                        if (pause_token.Token.IsPaused)
                                        {
                                            await pause_token.Token.WaitWhilePausedAsync(cancellation_token).ConfigureAwait(false);
                                            continue;
                                        }

                                        InFlightChunk? candidate = null;
                                        long oldestProgressTs = long.MaxValue;

                                        for (int i = 0; i < totalChunks; i++)
                                        {
                                            var c = Volatile.Read(ref inFlightChunks[i]);
                                            if (c != null && !c.IsCompleted && !c.IsHedged && c.LastProgressTimestamp < oldestProgressTs)
                                            {
                                                oldestProgressTs = c.LastProgressTimestamp;
                                                candidate = c;
                                            }
                                        }

                                        if (candidate == null)
                                        {
                                            break;
                                        }

                                        double elapsedMs = Stopwatch.GetElapsedTime(candidate.StartTimestamp).TotalMilliseconds;
                                        double stallMs = Stopwatch.GetElapsedTime(candidate.LastProgressTimestamp).TotalMilliseconds;
                                        long committed = candidate.BytesCommitted;
                                        long remainingBytes = candidate.TotalBytes - committed;

                                        bool isStalled = stallMs >= _config.TailHedgeDelayMs;
                                        bool isSeverelySlow = false;

                                        if (!isStalled && elapsedMs >= slowCheckMinElapsedMs && remainingBytes >= minStealBytesForSlowCheck)
                                        {
                                            long doneBytes = Volatile.Read(ref completedBytesTotal);
                                            long doneTicks = Volatile.Read(ref completedDurationTicksTotal);
                                            if (doneBytes > 0 && doneTicks > 0)
                                            {
                                                double avgBytesPerSec = doneBytes / ((double)doneTicks / Stopwatch.Frequency);
                                                double chunkBytesPerSec = committed / Math.Max(elapsedMs / 1000.0, 0.001);
                                                isSeverelySlow = chunkBytesPerSec < avgBytesPerSec * 0.25;
                                            }
                                        }

                                        if (!isStalled && !isSeverelySlow)
                                        {
                                            int waitMs = Math.Max(10, Math.Min(_config.TailHedgeDelayMs - (int)stallMs, 100));
                                            try
                                            {
                                                await Task.Delay(waitMs, candidate.Cts.Token).ConfigureAwait(false);
                                            }
                                            catch (OperationCanceledException) when (candidate.IsCompleted && !cancellation_token.IsCancellationRequested)
                                            {
                                                continue;
                                            }
                                            continue;
                                        }

                                        if (pause_token.Token.IsPaused || !candidate.TryClaimHedge())
                                        {
                                            continue;
                                        }

                                        // Re-read committed bytes right after claiming hedge to steal only the remaining tail
                                        long stealOffset = candidate.BytesCommitted;
                                        long stealStart = candidate.Piece.start + stealOffset;
                                        if (stealStart > candidate.Piece.end)
                                        {
                                            continue;
                                        }

                                        try
                                        {
                                            await octaneClient.Download(
                                                effectiveUri,
                                                (stealStart, candidate.Piece.end),
                                                fileHandle,
                                                headers,
                                                candidate.Cts.Token,
                                                pause_token.Token,
                                                chunkProgress: null).ConfigureAwait(false);
                                            CompleteChunk(candidate);
                                        }
                                        catch (OperationCanceledException) when (candidate.IsCompleted && !cancellation_token.IsCancellationRequested)
                                        {
                                            // Original worker won the race
                                        }
                                        catch when (!candidate.IsCompleted && !cancellation_token.IsCancellationRequested)
                                        {
                                            // Hedged attempt failed; allow original worker to finish normally
                                        }
                                    }
                                }
                        }

                        var workerTasks = new Task[workerCount];
                        for (int w = 0; w < workerCount; w++)
                        {
                            workerTasks[w] = RunWorkerAsync(w == 0);
                        }

                        await Task.WhenAll(workerTasks).ConfigureAwait(false);
                        success = true;
                    }
                    catch (AggregateException aggEx)
                    {
                        // Attempts to preserve the stack trace while only throwing the inner exception
                        var innerCause = Helpers.GetFirstRealException(aggEx);
                        ExceptionDispatchInfo.Capture(innerCause).Throw();
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                    finally
                    {
                        for (int i = 0; i < inFlightChunks.Length; i++)
                        {
                            inFlightChunks[i]?.Dispose();
                        }
                        pbar?.Dispose();
                    }
                }
                else
                {
                    LogUsingDefaultClientToDownloadFile();
                    try
                    {
                        await defaultClient.WriteResponseAsync(initialResponse, fileHandle, cancellation_token, pause_token.Token).ConfigureAwait(false);
                        success = true;
                    }
                    catch (Exception)
                    {
                        success = false;
                        throw;
                    }
                }
                    
                stopwatch.Stop();
                if (success)
                {
                    LogFileDownloadedSuccessfullyInStopwatchelapsedmillisecondsMs(stopwatch.ElapsedMilliseconds);
                }
            }
        }
        catch (Exception ex)
        {
            success = false;
            LogErrorDownloadingFileWithClienttypeClientEx(clientType, ex);
            throw;
        }
        finally
        {
            if (!success && !string.IsNullOrEmpty(filename) && File.Exists(filename))
            {
                File.Delete(filename);
            }
            Cleanup(stopwatch, _config, success);
        }
    }

    public void Dispose()
    {
        _factory?.Dispose();
        if (_clientFactory is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    [LoggerMessage(LogLevel.Trace, "Calling callback function...")]
    partial void LogCallingCallbackFunction();

    [LoggerMessage(LogLevel.Error, "Download Failed.")]
    partial void LogDownloadFailed();

    [LoggerMessage(LogLevel.Information, "Range supported: {range}")]
    partial void LogRangeSupportedRange(bool range);

    [LoggerMessage(LogLevel.Information, "Server file name: {filename}.")]
    partial void LogServerFileNameFilename(string filename);

    [LoggerMessage(LogLevel.Information, "TOTAL SIZE: {length}")]
    partial void LogTotalSizeLength(string length);

    [LoggerMessage(LogLevel.Debug, "Using Octane Client to download file.")]
    partial void LogUsingOctaneClientToDownloadFile();

    [LoggerMessage(LogLevel.Debug, "Using Default Client to download file.")]
    partial void LogUsingDefaultClientToDownloadFile();

    [LoggerMessage(LogLevel.Error, "Error Downloading File with {clientType} client")]
    partial void LogErrorDownloadingFileWithClienttypeClientEx(ClientType clientType, Exception exception);

    [LoggerMessage(LogLevel.Information, "File downloaded successfully in {StopwatchElapsedMilliseconds} ms.")]
    partial void LogFileDownloadedSuccessfullyInStopwatchelapsedmillisecondsMs(long StopwatchElapsedMilliseconds);
}