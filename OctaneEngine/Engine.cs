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
using OctaneEngineCore;
using OctaneEngineCore.Clients;
using OctaneEngineCore.ShellProgressBar;

// ReSharper disable All

namespace OctaneEngine
{
    public class Engine: IEngine
    {
        private readonly ILoggerFactory _factory;
        private readonly OctaneConfiguration _config;
        private readonly IClient _client;
        private readonly IClient _normalClient;

        public Engine(ILoggerFactory factory, OctaneConfiguration config, IClient client, IClient normalClient)
        {
            _factory = factory;
            _config = config;
            _client = client;
            _normalClient = normalClient;
        }
        
        #region Helpers
        private void Cleanup(ILogger logger, Stopwatch stopwatch, OctaneConfiguration config, bool success)
        {
            stopwatch.Stop();
            logger.LogInformation($"File downloaded in {stopwatch.ElapsedMilliseconds} ms.");
            logger.LogTrace("Calling callback function...");
            if (!success)
            {
                logger.LogError("Download Failed.");
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
        public async static Task<int> GetOptimalNumberOfParts(string url, NetworkAnalyzer.TestFileSize sizeToUse = NetworkAnalyzer.TestFileSize.Small)
        {
            if (!Enum.IsDefined(typeof(NetworkAnalyzer.TestFileSize), sizeToUse))
                throw new InvalidEnumArgumentException(nameof(sizeToUse), (int)sizeToUse,
                    typeof(NetworkAnalyzer.TestFileSize));
            using var client = new HttpClient();
            var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            var size_of_file = response.Content.Headers.ContentLength ?? 0;
            var networkSpeed = await NetworkAnalyzer.GetNetworkSpeed(NetworkAnalyzer.GetTestFile(sizeToUse));
            var networkLatency = await NetworkAnalyzer.GetNetworkLatency();
            int chunkSize = (int)Math.Ceiling(Math.Sqrt((double)networkSpeed * networkLatency));
            int numParts = (int)Math.Ceiling((double)size_of_file / chunkSize);
            numParts = Math.Min(numParts, Environment.ProcessorCount);
            return numParts;
        }
        
        private async Task<(long, bool)> getFileSizeAndRangeSupport(string url)
        {
            using var client = new HttpClient();
            var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            var responseLength = response.Content.Headers.ContentLength ?? 0;
            var rangeSupported = response.Headers.AcceptRanges.Contains("bytes");
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
        /// <param name="proxy"></param>
        public void SetProxy(IWebProxy proxy)
        {
            _config.Proxy = proxy;
        }
        
        /// <summary>
        ///     The core octane download function. 
        /// </summary>
        /// <param name="url">The string url of the file to be downloaded.</param>
        /// <param name="outFile">The output file name of the download. Use 'null' to get file name from url.</param>
        /// <param name="pauseTokenSource">The pause token source to use for pausing and resuming.</param>
        /// <param name="cancelTokenSource">The cancellation token for canceling the task.</param>
        public async Task DownloadFile(string url, string outFile = null, PauseTokenSource pauseTokenSource = null, CancellationTokenSource cancelTokenSource = null)
        {
            var stopwatch = new Stopwatch();
            var logger = _factory.CreateLogger<Engine>();
            var success = false;
            var filename = string.Empty;
            var clientType = string.Empty;
            
            try
            {
                var (_length, _range) = await getFileSizeAndRangeSupport(url);
            
                #region Varible Initilization
                    filename = outFile ?? Path.GetFileName(new Uri(url).LocalPath);
                    var cancellation_token = Helpers.CreateCancellationToken(cancelTokenSource, _config);
                    var pause_token = pauseTokenSource ?? new PauseTokenSource(_factory);
                    var memPool = ArrayPool<byte>.Create(_config.BufferSize, _config.Parts);
                    logger.LogInformation("Range supported: {range}", _range);
                    var partSize = Convert.ToInt64(_length / _config.Parts);
                    int tasksDone = 0;
                #endregion
            
                #region ServicePoint Configuration
                    logger.LogInformation("Server file name: {filename}.", filename);
                    ServicePointManager.Expect100Continue = false;
                    ServicePointManager.DefaultConnectionLimit = 10000;
                    ServicePointManager.SetTcpKeepAlive(true, Int32.MaxValue,1);
                    ServicePointManager.MaxServicePoints = _config.Parts;
                    #if NET6_0_OR_GREATER
                        ServicePointManager.ReusePort = true;
                    #endif
                #endregion
            
                logger.LogInformation("TOTAL SIZE: {length}", NetworkAnalyzer.PrettySize(_length));
                logger.LogInformation("PART SIZE: {partSize}", NetworkAnalyzer.PrettySize(partSize));
            
                stopwatch.Start();
                _client.SetBaseAddress(url);
            
                using (var mmf = MemoryMappedFile.CreateFromFile(filename, FileMode.OpenOrCreate, null, _length, MemoryMappedFileAccess.ReadWrite))
                {
                    //Check if range is supported
                    if (_client.IsRangeSupported())
                    {
                        clientType = "Octane";
                        var pieces = Helpers.CreatePartsList(_length, partSize, logger);
                        _client.SetMmf(mmf);
                        _client.SetArrayPool(memPool);
                        logger.LogInformation("Using Octane Client to download file.");
                        var options = new ParallelOptions()
                        {
                            MaxDegreeOfParallelism = Environment.ProcessorCount,
                            CancellationToken = cancellation_token,
                            //TaskScheduler = TaskScheduler.Current
                        };
                        ProgressBar pbar = null;
                        if (_config.ShowProgress)
                        {
                            pbar = new ProgressBar(pieces.Count * 2, "Downloading file...");
                            _client.SetProgressbar(pbar);
                        }

                        try
                        {
                            await Parallel.ForEachAsync(pieces, options, async (piece, token) =>
                            {
                                await _client.Download(url, piece, cancellation_token, pause_token.Token);

                                Interlocked.Increment(ref tasksDone);

                                logger.LogTrace("Finished {tasks}/{parts} pieces!", tasksDone, _config?.Parts);

                                pbar?.Tick();
                                _config?.ProgressCallback?.Invoke((double)tasksDone / _config.Parts);

                                success = true;
                                logger.LogInformation("File downloaded successfully.");
                            }).ConfigureAwait(false);
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
                        logger.LogInformation("Using Default Client to download file.");
                        clientType = "Normal";
                        try
                        {
                            await _normalClient.Download(url, (0, 0), cancellation_token, pause_token.Token);
                            success = true;
                        }
                        catch (Exception)
                        {
                            throw;
                        }

                        logger.LogInformation("File downloaded successfully.");
                    }
                }
            }
            catch (Exception ex)
            {
                success = false;
                logger.LogError(ex, "Error Downloading File with {clientType} client: {ex}", clientType, ex);
            }
            finally
            {
                if (!success)
                {
                    File.Delete(filename);
                }
                Cleanup(logger, stopwatch, _config, success);
            }
        }
    }
}