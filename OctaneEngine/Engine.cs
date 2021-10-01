using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

// ReSharper disable All

namespace OctaneDownloadEngine
{
    public class OctaneEngine
    {
        public OctaneEngine()
        {
            ServicePointManager.DefaultConnectionLimit = 10000;
        }

        private static int TasksDone = 0;

        #region Helper methods
        private static void CombineMultipleFilesIntoSingleFile(List<FileChunk> files, string outputFilePath)
        {
            Console.Title = string.Format("Number of files: {0}.", files.Count);
            using (var outputStream = File.Create(outputFilePath))
            {
                foreach (var inputFilePath in files)
                {
                    using (var inputStream = File.OpenRead(inputFilePath.TempFileName))
                    {
                        // Buffer size can be passed as the second argument.
                        outputStream.Position = inputFilePath.Start;
                        inputStream.CopyTo(outputStream);
                    }

                    Console.Title = string.Format("The file has been processed from {0} to {1}.", inputFilePath.Start,
                        inputFilePath.End);
                    File.Delete(inputFilePath.TempFileName);
                }
            }
        }
        #endregion

        public async static Task DownloadFile(string url, int parts, string outFile = null, Action<int> progressCallback = null)
        {
            var responseLength = (await WebRequest.Create(url).GetResponseAsync()).ContentLength;
            var partSize = (long) Math.Round(responseLength / parts + 0.0);
            var pieces = new List<FileChunk>();
            var filename = outFile;
            var defaultTitle = Console.Title;
            var uri = new Uri(url);

            Console.WriteLine(responseLength.ToString(CultureInfo.InvariantCulture) + " TOTAL SIZE");
            Console.WriteLine(partSize.ToString(CultureInfo.InvariantCulture) + " PART SIZE" + "\n");

            #region Outfile name
            if (outFile == null)
            {
                filename = Path.GetFileName(uri.LocalPath);
            }
            else
            {
                filename = outFile;
            }
            #endregion

            try
            {
                //Using our custom progressbar
                using (var progress = new ProgressBar())
                {
                    //Using custom concurrent queue to implement Enqueue and Dequeue Events
                    var asyncTasks = new EventfulConcurrentQueue<FileChunk>();

                    //Delegate for Dequeue
                    asyncTasks.ItemDequeued += delegate
                    {
                        //Tasks done holds the count of the tasks done
                        //Parts *2 because there are parts number of Enqueue AND Dequeue operations
                        if (progressCallback == null)
                        {
                            progress.Report(TasksDone / (parts * 2));
                            Thread.Sleep(20);
                        }
                        else
                        {
                            progressCallback?.Invoke(Convert.ToInt32(TasksDone / (parts * 2)));
                        }
                    };

                    //Delagate for Enqueue
                    asyncTasks.ItemEnqueued += delegate
                    {
                        if (progressCallback == null)
                        {
                            progress.Report(TasksDone / (parts * 2));
                            Thread.Sleep(20);
                        }
                        else
                        {
                            progressCallback?.Invoke(Convert.ToInt32(TasksDone / (parts * 2)));
                        }
                    };
                    
                    var HTTPpool = new ObjectPool<HttpClient>(() => new HttpClient(new RetryHandler(new HttpClientHandler(), 10)) {MaxResponseContentBufferSize = 1000000000});

                    //Variable to hold the old loop end
                    var previous = 0;

                    //Loop to add all the events to the queue
                    for (var i = (int) partSize; i <= responseLength; i += (int) partSize)
                    {
                        if (i + partSize < responseLength)
                        {
                            //Start and end values for the chunk
                            var start = previous;
                            var current_end = i;

                            pieces.Add(new FileChunk(start, current_end));

                            //Set the start of the next loop to be the current end
                            previous = current_end;
                        }
                        else
                        {
                            //Start and end values for the chunk
                            var start = previous;
                            var current_end = i;

                            pieces.Add(new FileChunk(start, (int) responseLength));

                            //Set the start of the next loop to be the current end
                            previous = current_end;
                        }
                    }

                    Parallel.For(0, pieces.Count, i =>
                    {
                        pieces[i].CreateTempFile();
                    });

                    Console.Title = "CHUNKS DONE";

                    var bufferBlock = new BufferBlock<FileChunk>();
                    var getStream = new TransformBlock<FileChunk, Tuple<Task<HttpResponseMessage>, FileChunk>>(piece =>
                        {
                            var client = HTTPpool.Get();
                            Console.Title = "STREAMING....";

                            //Open a http request with the range
                            var request = new HttpRequestMessage {RequestUri = new Uri(url)};
                            request.Headers.Range = new RangeHeaderValue(piece.Start, piece.End);

                            //Send the request
                            var downloadTask = new Task<HttpResponseMessage>(() => client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).Result);

                            //Use interlocked to increment Tasks done by one
                            Interlocked.Add(ref TasksDone, 1);
                            asyncTasks.Enqueue(piece);

                            downloadTask.ContinueWith(x =>
                            {
                                HTTPpool.Return(client);
                            }, TaskContinuationOptions.OnlyOnRanToCompletion);

                            return new Tuple<Task<HttpResponseMessage>, FileChunk>(downloadTask, piece);
                        }, new ExecutionDataflowBlockOptions
                        {
                            BoundedCapacity = -1, // Cap the item count
                            MaxDegreeOfParallelism = Environment.ProcessorCount, // Parallelize on all cores
                        }
                    );

                    
                    var writeStream = new ActionBlock<Tuple<Task<HttpResponseMessage>, FileChunk>>(async task =>
                    {
                        Parallel.Invoke(() =>
                        {
                            task.Item1.Start();
                            task.Item1.Wait();
                        });

                        if (task.Item1.IsCompleted == true)
                        {
                            using (var streamToRead = await task.Item1.Result.Content.ReadAsStreamAsync())
                            {
                                using (var fileToWriteTo = File.Open(task.Item2.TempFileName, FileMode.Append, FileAccess.Write, FileShare.Write))
                                {
                                    streamToRead.CopyTo(fileToWriteTo);
                                    var s = new FileChunk();
                                    asyncTasks.TryDequeue(out s);
                                    Interlocked.Add(ref TasksDone, 1);
                                }
                                task.Item1.Result.Dispose();
                                task.Item1.Dispose();
                            }
                        }
                    }, new ExecutionDataflowBlockOptions
                    {
                        BoundedCapacity = -1, // Cap the item count
                        MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded, // Parallelize on all cores
                    });

                    //Propage errors and completion
                    var linkOptions = new DataflowLinkOptions {PropagateCompletion = true};

                    //Write the block ass soon as the stream is recieved
                    bufferBlock.LinkTo(getStream, linkOptions);
                    getStream.LinkTo(writeStream, linkOptions);

                    //Start every piece in pieces in parallel fashion
                    Parallel.ForEach(pieces, (chunk) => { bufferBlock.Post(chunk);});
                    bufferBlock.Complete();

                    //Wait for all the streams to be recieved
                    await getStream.Completion;

                    await writeStream.Completion.ContinueWith(x =>
                    {
                        //If all the tasks are done, Join the temp files
                        if (asyncTasks.Count == 0 && bufferBlock.Count == 0)
                        {
                            CombineMultipleFilesIntoSingleFile(pieces, filename);
                        }
                    });
                    if (bufferBlock.Count == 0)
                    {
                        getStream.Complete();
                        writeStream.Complete();
                    }

                    
                }

                //Restore the original title
                Console.Title = defaultTitle;

                //Force garbage collection
                GC.Collect(3);
                GC.WaitForPendingFinalizers();
                GC.Collect(3);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                //Delete the tempfiles if there's an error
                foreach (var piece in pieces)
                {
                    try
                    {
                        File.Delete(piece.TempFileName);
                    }
                    catch (Exception exx)
                    {
                        Console.WriteLine(exx.Message);
                        Console.ReadLine();
                    }
                }
            }
        }
    }
}