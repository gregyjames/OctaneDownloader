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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OctaneEngineCore;
using OctaneEngineCore.Clients;
using OctaneEngineCore.ColorConsoleLogger;
using OctaneEngineCore.ShellProgressBar;

// ReSharper disable All

namespace OctaneEngine
{
    public class Engine
    {
        private readonly ILoggerFactory _factory;
        private OctaneConfiguration _config;

        public Engine() { }
        public Engine(ILoggerFactory factory, OctaneConfiguration config)
        {
            _factory = factory;
            _config = config;
        }
        
        #region Helpers
        private static ILoggerFactory createLoggerFactory(ILoggerFactory loggerFactory)
        {
            var factory = loggerFactory ?? new LoggerFactory();
            if (loggerFactory == null)
            {
                factory.AddProvider(new ColorConsoleLoggerProvider(new ColorConsoleLoggerConfiguration()));
            }

            return factory;
        }
        private static OctaneConfiguration createConfiguration(OctaneConfiguration config, ILogger logger)
        {
            if (config == null)
            {
                logger.LogInformation("Octane config not providing, using default configuration.");
                config = new OctaneConfiguration();
            }

            logger.LogInformation($"CONFIGURATION: {config.ToString()}");

            return config;
        }
        private static CancellationToken createCancellationToken(CancellationTokenSource cancelTokenSource, OctaneConfiguration config, string outFile)
        {
            var cancellation_token = cancelTokenSource?.Token ?? new CancellationToken();
            cancellation_token.Register(new Action(() =>
            {
                config.DoneCallback?.Invoke(false);
                if (File.Exists(outFile))
                {
                    File.Delete(outFile);
                }
            }));

            return cancellation_token;
        }
        private static async Task<(long, bool)> getFileSizeAndRangeSupport(string url)
        {
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                var responseLength = response.Content.Headers.ContentLength ?? 0;
                var rangeSupported = response.Headers.AcceptRanges.Contains("bytes");

                return (responseLength, rangeSupported);
            }
            
        }
        private static HttpClient createHTTPClient(OctaneConfiguration config, ILoggerFactory factory)
        {
            var clientHandler = new HttpClientHandler()
            {
                PreAuthenticate = true,
                UseDefaultCredentials = true,
                Proxy = config.Proxy,
                UseProxy = config.UseProxy,
                MaxConnectionsPerServer = config.Parts,
                UseCookies = false,
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };

            var retryHandler = new RetryHandler(clientHandler, factory, config.NumRetries);

            var _client = new HttpClient(retryHandler)
            {
                MaxResponseContentBufferSize = config.BufferSize
            };

            return _client;
        }
        private static List<ValueTuple<long, long>> createPartsList(bool rangeSupported, long responseLength, long partSize, ILogger logger)
        {
            var pieces = new List<ValueTuple<long, long>>();
            //Loop to add all the events to the queue
            if (rangeSupported)
            {
                for (long i = 0; i < responseLength; i += partSize)
                {
                    //Increment the start by one byte for all parts but the first which starts from zero.
                    if (i != 0)
                    {
                        i += 1;
                    }
                    var j = Math.Min(i + partSize, responseLength);
                    pieces.Insert(0, new ValueTuple<long, long>(i, j));
                    logger.LogTrace($"Piece with range ({pieces.First().Item1},{pieces.First().Item2}) added to tasks queue.");
                }
            }

            return pieces;
        }
        private static ProgressBar createProgressBar(OctaneConfiguration config, ILogger logger)
        {
            //Options for progress base
            var options = new ProgressBarOptions
            {
                ProgressBarOnBottom = false,
                BackgroundCharacter = '\u2593',
                DenseProgressBar = false,
                DisplayTimeInRealTime = false
            };
            
            var pbar = config.ShowProgress
                ? new ProgressBar(config.Parts, "Downloading File...", options)
                : null;
            if (pbar == null)
            {
                logger.LogInformation("Progress bar disabled.");
            }

            return pbar;
        }
        private static void checkURL(string url, ILogger logger)
        {
            if (url == null)
            {
                logger.LogCritical("No URL provided (null value).");
                throw new ArgumentNullException(nameof(url));
            }
        }
        private static void Cleanup(GCLatencyMode old_mode, ILogger logger, Stopwatch stopwatch, HttpClient _client, OctaneConfiguration config, bool success)
        {
            GCSettings.LatencyMode = old_mode;
            logger.LogTrace("Restored GC mode.");
            _client.Dispose();
            stopwatch.Stop();
            logger.LogInformation($"File downloaded in {stopwatch.ElapsedMilliseconds} ms.");
            logger.LogTrace("Calling callback function...");
            config.DoneCallback?.Invoke(success);

            if (!success)
            {
                logger.LogError("Download Failed.");
            }
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
        
        /// <summary>
        ///     The core octane download function. 
        /// </summary>
        /// <param name="url">The string url of the file to be downloaded.</param>
        /// <param name="outFile">The output file name of the download. Use 'null' to get file name from url.</param>
        /// <param name="pauseTokenSource">The pause token source to use for pausing and resuming.</param>
        /// <param name="cancelTokenSource">The cancellation token for canceling the task.</param>
        public async Task DownloadFile(string url, string outFile = null, PauseTokenSource pauseTokenSource = null, CancellationTokenSource cancelTokenSource = null)
        {
            #region Varible Initilization
            var factory = createLoggerFactory(_factory);
            var logger = factory.CreateLogger("OctaneEngine");
            var old_mode = GCSettings.LatencyMode;
            logger.LogTrace("Setting GC to sustained low latency mode.");
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            var success = false;
            var cancellation_token = createCancellationToken(cancelTokenSource, _config, outFile);
            checkURL(url, logger);
            _config = createConfiguration(_config, logger);
            var pause_token = pauseTokenSource ?? new PauseTokenSource(factory);
            var filename = outFile ?? Path.GetFileName(new Uri(url).LocalPath);
            var stopwatch = new Stopwatch();
            var memPool = ArrayPool<byte>.Create(_config.BufferSize, _config.Parts);
            var (responseLength, rangeSupported) = await getFileSizeAndRangeSupport(url);
            logger.LogInformation($"Range supported: {rangeSupported}");
            var partSize = Convert.ToInt64(responseLength / _config.Parts);
            var pieces = createPartsList(rangeSupported, responseLength, partSize, logger);
            var _client = createHTTPClient(_config, factory);
            int tasksDone = 0;
            #endregion
            
            #region ServicePoint Configuration
            logger.LogInformation($"Server file name: {filename}.");
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.DefaultConnectionLimit = 10000;
            ServicePointManager.SetTcpKeepAlive(true, Int32.MaxValue,1);
            ServicePointManager.MaxServicePoints = _config.Parts;
            #if NET6_0_OR_GREATER
                ServicePointManager.ReusePort = true;
            #endif
            #endregion
            
            logger.LogInformation($"TOTAL SIZE: {NetworkAnalyzer.prettySize(responseLength)}");
            logger.LogInformation($"PART SIZE: {NetworkAnalyzer.prettySize(partSize)}");
            
            stopwatch.Start();
            
            //Create memory mapped file to hold the file
            using (var mmf = MemoryMappedFile.CreateFromFile(filename, FileMode.OpenOrCreate, null, responseLength, MemoryMappedFileAccess.ReadWrite))
            {
                try
                {
                    var pbar = createProgressBar(_config, logger);

                    //Create a client based on if range is supported or not..
                    using (IClient client = rangeSupported ? new OctaneClient(_config, _client, factory, mmf, pbar, memPool) : new DefaultClient(_client, mmf, memPool, _config, pbar, partSize))
                    {
                        //Check if range is supported
                        if (rangeSupported)
                        {
                            logger.LogInformation("Using Octane Client to download file.");
                            //No GC Mode if supported
                            #if NET6_0_OR_GREATER
                                GC.TryStartNoGCRegion(Environment.ProcessorCount*_config.BufferSize, true);
                            #endif
                            try
                            {
                                await Parallel.ForEachAsync(pieces, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = cancellation_token, TaskScheduler = TaskScheduler.Current}, async (piece, token) =>
                                    {
                                        var message = await client.SendMessage(url, piece, token, pause_token.Token);
                                        await client.ReadResponse(message, piece, token, pause_token.Token);

                                        Interlocked.Increment(ref tasksDone);
                                        if (_config?.ProgressCallback != null)
                                        {
                                            _config?.ProgressCallback((tasksDone + 0.0) / (pieces.Count + 0.0));
                                        }

                                        logger.LogTrace($"Finished {tasksDone}/{_config?.Parts} pieces!");
                                    });
                            }
                            catch(Exception ex)
                            {
                                logger.LogError($"ERROR USING CORE CLIENT: {ex.Message}");
                            }
                            finally
                            {
                                #if NET6_0_OR_GREATER
                                    GC.EndNoGCRegion();
                                #endif
                            }
                        }
                        else
                        {
                            logger.LogInformation("Using Default Client to download file.");
                            var message = await client.SendMessage(url, (0, 0), cancellation_token, pause_token.Token);
                            await client.ReadResponse(message, (0, 0), cancellation_token, pause_token.Token);
                        }
                    }
                    
                    success = true;
                    logger.LogInformation("File downloaded successfully.");
                }
                catch (Exception ex)
                {
                    if (ex.GetType().FullName == "System.InvalidOperationException")
                    {
                        logger.LogInformation("Allocated size too small but file downloaded successfully.");
                        success = true;
                    }
                    else
                    {
                        logger.LogError($"{ex.GetType().FullName}: {ex.Message}");
                        success = false;
                    }
                }
                finally
                {
                    Cleanup(old_mode, logger, stopwatch, _client, _config, success);
                }
            }
        }
    }
}