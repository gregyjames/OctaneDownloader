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
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Text;
using Microsoft.Extensions.Logging;
using OctaneEngineCore;
using OctaneEngineCore.Clients;
using OctaneEngineCore.ColorConsoleLogger;
using OctaneEngineCore.ShellProgressBar;

// ReSharper disable All

namespace OctaneEngine
{
    public static class Engine
    {
        #region Helpers
        private static readonly string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        private static string prettySize(long len)
        {
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len >> 10;
            }
            
            string result = ZString.Format("{0:0.##} {1}", len, sizes[order]); 
            
            return result;
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
            config.DoneCallback.Invoke(success);

            if (!success)
            {
                logger.LogError("Download Failed.");
            }
        }
        #endregion
        
        /// <summary>
        ///     The core octane download function.
        /// </summary>
        /// <param name="url">The string url of the file to be downloaded.</param>
        /// <param name="outFile">The output file name of the download. Use 'null' to get file name from url.</param>
        /// <param name="factory">The ILoggerFactory instance to use for logging.</param>
        /// <param name="config">The OctaneConfiguration object used for configuring the downloader.</param>
        /// <param name="pauseTokenSource">The pause token source to use for pausing and resuming.</param>
        /// <param name="cancelTokenSource">The cancellation token for canceling the task.</param>
        public async static Task DownloadFile(string url, ILoggerFactory loggerFactory = null, string outFile = null, OctaneConfiguration config = null, PauseTokenSource pauseTokenSource = null, CancellationTokenSource cancelTokenSource = null)
        {
            #region Varible Initilization
            var success = false;
            var cancellation_token = createCancellationToken(cancelTokenSource, config, outFile);
            var factory = createLoggerFactory(loggerFactory);
            var logger = factory.CreateLogger("OctaneEngine");
            checkURL(url, logger);
            config = createConfiguration(config, logger);
            var pause_token = pauseTokenSource ?? new PauseTokenSource(factory);
            var filename = outFile ?? Path.GetFileName(new Uri(url).LocalPath);
            var old_mode = GCSettings.LatencyMode;
            var stopwatch = new Stopwatch();
            var memPool = ArrayPool<byte>.Shared;
            var (responseLength, rangeSupported) = await getFileSizeAndRangeSupport(url);
            logger.LogInformation($"Range supported: {rangeSupported}");
            var partSize = Convert.ToInt64(responseLength / config.Parts);
            var pieces = createPartsList(rangeSupported, responseLength, partSize, logger);
            var _client = createHTTPClient(config, factory);
            int tasksDone = 0;
            IClient client = null;
            #endregion
            
            #region ServicePoint Configuration
            logger.LogInformation($"Server file name: {filename}.");
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.DefaultConnectionLimit = 10000;
            ServicePointManager.SetTcpKeepAlive(true, Int32.MaxValue,1);
            ServicePointManager.MaxServicePoints = config.Parts;
            #if NET6_0_OR_GREATER
                ServicePointManager.ReusePort = true;
            #endif
            #endregion
            
            logger.LogInformation($"TOTAL SIZE: {prettySize(responseLength)}");
            logger.LogInformation($"PART SIZE: {prettySize(partSize)}");
            
            stopwatch.Start();
            //Create memory mapped file to hold the file
            using (var mmf = MemoryMappedFile.CreateFromFile(filename, FileMode.OpenOrCreate, null, responseLength, MemoryMappedFileAccess.ReadWrite))
            {
                try
                {
                    GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
                    logger.LogTrace("Setting GC to sustained low latency mode.");
                    var pbar = createProgressBar(config, logger);

                    if (rangeSupported)
                    {
                        logger.LogInformation("Using Octane Client to download file.");
                        GC.TryStartNoGCRegion(responseLength, true);
                        await Parallel.ForEachAsync(pieces, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, async (piece, t) =>
                            {
                                //Get a client from the pool and request for the content range
                                client = new OctaneClient(config, _client, factory, mmf, pbar, memPool);
                                
                                //Request headers so we dont cache the file into memory
                                var message = await client.SendMessage(url, piece, cancellation_token, pause_token.Token);
                                await client.ReadResponse(message, piece, cancellation_token, pause_token.Token);

                                Interlocked.Increment(ref tasksDone);
                                if (config?.ProgressCallback != null)
                                {
                                    config?.ProgressCallback((tasksDone + 0.0) / (pieces.Count + 0.0));
                                }

                                logger.LogTrace($"Finished {tasksDone - 1}/{config.Parts} pieces!");
                            });
                        GC.EndNoGCRegion();
                    }
                    else
                    {
                        logger.LogInformation("Using Default Client to download file.");
                        client = new DefaultClient(_client, mmf);
                        var message = await client.SendMessage(url, (0, 0), cancellation_token, pause_token.Token);
                        await client.ReadResponse(message, (0, 0), cancellation_token, pause_token.Token);
                    }

                    success = true;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex.Message);
                    success = false;
                }
                finally
                {
                    Cleanup(old_mode, logger, stopwatch, _client, config, success);
                }
            }
        }
    }
}