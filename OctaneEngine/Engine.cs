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
using System.Threading;
using System.Threading.Tasks;
using Collections.Pooled;
using Microsoft.Extensions.Logging;
using OctaneEngineCore;
using OctaneEngineCore.Clients;

// ReSharper disable All

namespace OctaneEngine
{
    public class Engine: IEngine
    {
        private readonly ILoggerFactory _factory;
        private readonly OctaneConfiguration _config;
        private readonly IClient _client;
        private readonly long _length;
        private readonly bool _range;
        public Engine(ILoggerFactory factory, OctaneConfiguration config, IClient client, long length, bool rangeSupported)
        {
            _factory = factory;
            _config = config;
            _client = client;
            _length = length;
            _range = rangeSupported;
        }
        
        #region Helpers
        private CancellationToken CreateCancellationToken(CancellationTokenSource cancelTokenSource, OctaneConfiguration config, string outFile)
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
        private PooledList<ValueTuple<long, long>> CreatePartsList(long responseLength, long partSize, ILogger logger)
        {
            var pieces = new PooledList<ValueTuple<long, long>>();
            //Loop to add all the events to the queue
            for (long i = 0; i < responseLength; i += partSize) {
                //Increment the start by one byte for all parts but the first which starts from zero.
                if (i != 0) {
                    i += 1;
                }
                var j = Math.Min(i + partSize, responseLength);
                var piece = new ValueTuple<long, long>(i, j);
                pieces.Add(piece);
                logger.LogTrace($"Piece with range ({piece.Item1},{piece.Item2}) added to tasks queue.");
            }
            
            return pieces;
        }
        private void Cleanup(ILogger logger, Stopwatch stopwatch, OctaneConfiguration config, bool success)
        {
            logger.LogTrace("Restored GC mode.");
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
                var logger = _factory.CreateLogger("OctaneEngine");
                var filename = outFile ?? Path.GetFileName(new Uri(url).LocalPath);
                var success = false;
                var cancellation_token = CreateCancellationToken(cancelTokenSource, _config, filename);
                var pause_token = pauseTokenSource ?? new PauseTokenSource(_factory);
                var stopwatch = new Stopwatch();
                var mmf = MemoryMappedFile.CreateFromFile(filename, FileMode.OpenOrCreate, null, _length, MemoryMappedFileAccess.ReadWrite);
                var memPool = ArrayPool<byte>.Create(_config.BufferSize, _config.Parts);
            
                logger.LogInformation($"Range supported: {_range}");
                var partSize = Convert.ToInt64(_length / _config.Parts);
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
            
            logger.LogInformation($"TOTAL SIZE: {NetworkAnalyzer.prettySize(_length)}");
            logger.LogInformation($"PART SIZE: {NetworkAnalyzer.prettySize(partSize)}");
            
            stopwatch.Start();
            
            try
            {
                //Check if range is supported
                if (_client.isRangeSupported())
                {
                    var pieces = CreatePartsList(_length, partSize, logger);
                    _client.SetMMF(mmf);
                    _client.SetArrayPool(memPool);
                    logger.LogInformation("Using Octane Client to download file.");
                    var options = new ParallelOptions()
                    {
                        MaxDegreeOfParallelism = Environment.ProcessorCount,
                        CancellationToken = cancellation_token,
                        TaskScheduler = TaskScheduler.Current
                    };
                    try
                    {
                        await Parallel.ForEachAsync(pieces,options, async (piece, token) => {
                            var message = await _client.SendMessage(url, piece, token, pause_token.Token).ConfigureAwait(false);
                            await _client.ReadResponse(message, piece, token, pause_token.Token).ConfigureAwait(false);
                                
                            Interlocked.Increment(ref tasksDone);
                            if (_config?.ProgressCallback != null) {
                                await Task.Run(() => _config?.ProgressCallback((tasksDone + 0.0) / (pieces.Count + 0.0)), token).ConfigureAwait(false);
                            }
                            logger.LogTrace($"Finished {tasksDone}/{_config?.Parts} pieces!");
                        }).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"ERROR USING CORE CLIENT: {ex.Message}");
                    }
                }
                else
                {
                    logger.LogInformation("Using Default Client to download file.");
                    var message = await _client.SendMessage(
                        url, 
                        (0, 0), 
                        cancellation_token, 
                        pause_token.Token);
                    await _client.ReadResponse(
                        message, 
                        (0, 0), 
                        cancellation_token, 
                        pause_token.Token);
                }

                success = true;
                logger.LogInformation("File downloaded successfully.");
            }
            catch (Exception ex)
            {
                logger.LogError($"{ex.GetType().FullName}: {ex.Message}");
                success = false;
            }
            finally
            {
                Cleanup(logger, stopwatch, _config, success);
            }
        }
    }
}