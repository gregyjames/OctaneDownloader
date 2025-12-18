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
using System.IO.MemoryMappedFiles;
using System.Net;
using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OctaneEngineCore.Clients;
using OctaneEngineCore.Implementations.NetworkAnalyzer;
using OctaneEngineCore.Interfaces;
using OctaneEngineCore.ShellProgressBar;

// ReSharper disable All

namespace OctaneEngineCore.Implementations;

public class Engine: IEngine, IDisposable
{
    private readonly ILoggerFactory _factory;
    private OctaneClient? _client;
    private DefaultClient? _defaultClient;
    private OctaneConfiguration _config;
    private readonly ILogger<Engine> _logger;
    private readonly OctaneHTTPClientPool _clientFactory;
    public Engine(IOptions<OctaneConfiguration> config, OctaneHTTPClientPool clientFactory, ILoggerFactory factory)
    {
        _clientFactory = clientFactory;
        _factory = factory ?? NullLoggerFactory.Instance;
        _logger = _factory.CreateLogger<Engine>();
        _config = config.Value;
        _clientFactory = clientFactory;
    }

    /// <summary>
    /// Creates a new Engine instance without dependency injection
    /// </summary>
    internal Engine(OctaneClient? client, DefaultClient? defaultClient, OctaneHTTPClientPool clientFactory,
        OctaneConfiguration config, ILoggerFactory? factory = null)
    {
        _factory = factory ?? NullLoggerFactory.Instance;
        _logger = _factory.CreateLogger<Engine>();
        _client = client;
        _defaultClient = defaultClient;
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _clientFactory = new OctaneHTTPClientPool(_config, _factory);
    }
        
    #region Helpers
    private void Cleanup(Stopwatch stopwatch, OctaneConfiguration config, bool success)
    {
        _logger.LogTrace("Calling callback function...");
        if (!success)
        {
            _logger.LogError("Download Failed.");
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
        numParts = Math.Min(numParts, Environment.ProcessorCount);
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
    
    private async Task<(long, bool)> getFileSizeAndRangeSupport(string url)
    {
        var client = _clientFactory.Rent(OctaneHTTPClientPool.DEFAULT_CLIENT_NAME);
        var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        var responseLength = response.Content.Headers.ContentLength ?? 0;
        var rangeSupported = response.Headers.AcceptRanges.Contains("bytes");
        _clientFactory.Return(OctaneHTTPClientPool.DEFAULT_CLIENT_NAME, client);
        return (responseLength, rangeSupported);
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
        var clientType = string.Empty;
        HttpClient? client = null;
        client = _clientFactory.Rent(OctaneHTTPClientPool.DEFAULT_CLIENT_NAME);
        _client = new OctaneClient(_config, client, _factory);
        _defaultClient = new DefaultClient(client, _config);

        try
        {
            var (_length, _range) = await getFileSizeAndRangeSupport(request.Url);
            
            #region Varible Initilization
            filename = request.OutFile ?? Path.GetFileName(new Uri(request.Url).LocalPath);
            var cancellation_token = token;
            var pause_token = pauseTokenSource ?? new PauseTokenSource(_factory);
            var memPool = ArrayPool<byte>.Create(_config.BufferSize, _config.Parts);
            _logger.LogInformation("Range supported: {range}", _range);
            int tasksDone = 0;
            #endregion
            
            #region ServicePoint Configuration
            _logger.LogInformation("Server file name: {filename}.", filename);
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.DefaultConnectionLimit = 10000;
            ServicePointManager.SetTcpKeepAlive(true, Int32.MaxValue,1);
            ServicePointManager.MaxServicePoints = _config.Parts;
#if NET6_0_OR_GREATER
            ServicePointManager.ReusePort = true;
#endif
            #endregion
            
            _logger.LogInformation("TOTAL SIZE: {length}", NetworkAnalyzer.NetworkAnalyzer.PrettySize(_length));
                
            stopwatch.Start();
            
            using (var mmf = MemoryMappedFile.CreateFromFile(filename, FileMode.OpenOrCreate, null, _length, MemoryMappedFileAccess.ReadWrite))
            {
                //Check if range is supported
                if (_range)
                {
                    clientType = "Octane";
                    var pieces = Helpers.CreatePartsList(_length, _config.Parts, _logger);
                    _client.SetMmf(mmf);
                    _client.SetArrayPool(memPool);
                    _logger.LogDebug("Using Octane Client to download file.");
                    var options = new ParallelOptions()
                    {
                        MaxDegreeOfParallelism = Environment.ProcessorCount,
                        CancellationToken = cancellation_token,
                        //TaskScheduler = TaskScheduler.Current
                    };
                    ProgressBar? pbar = null;
                    
                    if (_config.ShowProgress)
                    {
                        pbar = new ProgressBar(pieces.Count * 2, "Downloading file...");
                        _client.SetProgressbar(pbar);
                        _defaultClient.SetProgressbar(pbar);
                    }

                    try
                    {
                        await Parallel.ForEachAsync(pieces, options, async (piece, token) =>
                        {
                            await _client.Download(request.Url, piece, request.Headers, cancellation_token, pause_token.Token);

                            Interlocked.Increment(ref tasksDone);
                                
                            pbar?.Tick();
                            _config?.ProgressCallback?.Invoke((double)tasksDone / _config.Parts);
                        }).ConfigureAwait(false);
                            
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
                }
                else
                {
                    _logger.LogDebug("Using Default Client to download file.");
                    clientType = "Normal";
                    _defaultClient.SetMmf(mmf);
                    _defaultClient.SetArrayPool(memPool);
                    try
                    {
                        await _defaultClient.Download(request.Url, (0, 0), request.Headers, cancellation_token, pause_token.Token).ConfigureAwait(false);
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
                    _logger.LogInformation($"File downloaded successfully in {stopwatch.ElapsedMilliseconds} ms.");
                }
            }
        }
        catch (Exception ex)
        {
            success = false;
            _logger.LogError(ex, "Error Downloading File with {clientType} client: {ex}", clientType, ex);
            throw;
        }
        finally
        {
            if (!success)
            {
                File.Delete(filename);
            }
            Cleanup(stopwatch, _config, success);
            _clientFactory.Return(OctaneHTTPClientPool.DEFAULT_CLIENT_NAME, client);
        }
    }

    public void Dispose()
    {
        _factory?.Dispose();
        _clientFactory.Dispose();
    }
}