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
using System.Net.Http.Headers;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Text;
using Microsoft.Extensions.Logging;
using OctaneEngineCore;
using OctaneEngineCore.ShellProgressBar;

// ReSharper disable All

namespace OctaneEngine
{
    public static class Engine
    {
        static readonly string[] sizes = { "B", "KB", "MB", "GB", "TB" };

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

        /// <summary>
        ///     The core octane download function.
        /// </summary>
        /// <param name="url">The string url of the file to be downloaded.</param>
        /// <param name="outFile">The output file name of the download. Use 'null' to get file name from url.</param>
        /// <param name="loggerFactory">The ILoggerFactory instance to use for logging.</param>
        /// <param name="config">The OctaneConfiguration object used for configuring the downloader.</param>
        public async static Task DownloadFile(string url, ILoggerFactory loggerFactory = null, string outFile = null,
            OctaneConfiguration config = null, PauseTokenSource pauseTokenSource = null)
        {
            var stopwatch = new Stopwatch();

            loggerFactory ??= new LoggerFactory();
            
            var logger = loggerFactory.CreateLogger("OctaneEngine");

            stopwatch.Start();
            if (config == null)
            {
                logger.LogInformation("Octane config not providing, using default configuration.");
                config = new OctaneConfiguration();
            }

            logger.LogInformation($"CONFIGURATION: {config.ToString()}");

            var old_mode = GCSettings.LatencyMode;
            if (url == null)
            {
                logger.LogCritical("No URL provided (null value).");
                throw new ArgumentNullException(nameof(url));
            }

            var memPool = ArrayPool<byte>.Shared;

            //Get response length and calculate part sizes
            var responseLength = (await WebRequest.Create(url).GetResponseAsync()).ContentLength;
            var rangeSupported = (await WebRequest.Create(url).GetResponseAsync()).Headers["Accept-Ranges"] == "bytes";
            logger.LogInformation($"Range supported: {rangeSupported}");
            var partSize = (long)Math.Floor(responseLength / (config.Parts + 0.0));
            var pieces = new List<ValueTuple<long, long>>();
            var uri = new Uri(url);
            var filename = outFile ?? Path.GetFileName(uri.LocalPath);

            logger.LogInformation($"Server file name: {filename}.");
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.DefaultConnectionLimit = 10000;
            ServicePointManager.FindServicePoint(new Uri(url)).ConnectionLimit = config.Parts;

            #if NET6_0_OR_GREATER
                ServicePointManager.ReusePort = true;
            #endif

            logger.LogInformation($"TOTAL SIZE: {prettySize(responseLength)}");
            logger.LogInformation($"PART SIZE: {prettySize(partSize)}");

            //Loop to add all the events to the queue
            if (rangeSupported)
            {
                for (long i = 0; i < responseLength; i += partSize)
                {
                    pieces.Insert(0,
                        (i + partSize < responseLength
                            ? new ValueTuple<long, long>(i, i + partSize)
                            : new ValueTuple<long, long>(i, responseLength)));
                    logger.LogTrace(
                        $"Piece with range ({pieces.First().Item1},{pieces.First().Item2}) added to tasks queue.");
                }
            }

            //Options for progress base
            var options = new ProgressBarOptions
            {
                ProgressBarOnBottom = false,
                BackgroundCharacter = '\u2593',
                DenseProgressBar = false,
                DisplayTimeInRealTime = false
            };

            #region HTTPClient Init

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

            var retryHandler = new RetryHandler(clientHandler, config.NumRetries, loggerFactory);

            var _client = new HttpClient(retryHandler)
            {
                MaxResponseContentBufferSize = config.BufferSize
            };

            #endregion

            //Create memory mapped file to hold the file
            using (var mmf = MemoryMappedFile.CreateFromFile(filename, FileMode.OpenOrCreate, null, responseLength,
                       MemoryMappedFileAccess.ReadWrite))
            {
                int tasksDone = 0;
                try
                {
                    GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
                    logger.LogTrace("Setting GC to sustained low latency mode.");
                    var pbar = config.ShowProgress
                        ? new ProgressBar(config.Parts, "Downloading File...", options)
                        : null;
                    if (pbar == null)
                    {
                        logger.LogInformation("Progress bar disabled.");
                    }

                    IClient client = null;

                    if (rangeSupported)
                    {
                        logger.LogInformation("Using Octane Client to download file.");
                        await Parallel.ForEachAsync(pieces,
                            new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                            async (piece, cancellationToken) =>
                            {
                                Trace.Listeners.Clear();

                                //Get a client from the pool and request for the content range
                                client = new OctaneClient(config, _client, loggerFactory, mmf, pbar, memPool);

                                using (var request = new HttpRequestMessage { RequestUri = new Uri(url) })
                                {
                                    request.Headers.Range = new RangeHeaderValue(piece.Item1, piece.Item2);

                                    //Request headers so we dont cache the file into memory
                                    if (client != null)
                                    {
                                        var message = client.SendMessage(url, piece, cancellationToken, pauseTokenSource.Token).Result;
                                        await client.ReadResponse(message, piece, cancellationToken, pauseTokenSource.Token);
                                    }
                                    else
                                    {
                                        logger.LogCritical("Error creating client.");
                                    }

                                    Interlocked.Increment(ref tasksDone);
                                    config?.ProgressCallback((double)((tasksDone + 0.0) / (pieces.Count + 0.0)));
                                    logger.LogTrace($"Finished {tasksDone - 1}/{config.Parts} pieces!");
                                }
                            });
                    }
                    else
                    {
                        logger.LogInformation("Using Default Client to download file.");
                        client = new DefaultClient(_client, mmf);
                        var cancellationToken = new CancellationToken();
                        var message = client.SendMessage(url, (0, 0), cancellationToken, pauseTokenSource.Token).Result;
                        await client.ReadResponse(message, (0, 0), cancellationToken, pauseTokenSource.Token);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex.Message);
                    config.DoneCallback(false);
                }
                finally
                {
                    GCSettings.LatencyMode = old_mode;
                    logger.LogTrace("Restored GC mode.");
                    _client.Dispose();
                    stopwatch.Stop();
                    logger.LogInformation($"File downloaded in {stopwatch.ElapsedMilliseconds} ms.");
                    logger.LogTrace("Calling callback function...");
                    config.DoneCallback(true);
                }
            }
        }
    }
}