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
using System.Runtime.CompilerServices;
using SafeObjectPool;

// ReSharper disable All

namespace OctaneDownloadEngine
{
public class OctaneEngine
{
    #region Constructor
    public OctaneEngine()
    {
        ServicePointManager.DefaultConnectionLimit = 10000;
        TasksDone = 0;
    }
    #endregion

    #region Variables
    private static int TasksDone = 0;
    private static long responseLength = 0;
    private ObjectPool<HttpClient> _wcPool = new ObjectPool<HttpClient>(10, () =>
    {
        // GetResponseAsync deadlocks for some reason so switched to HttpClient instead
        var client = new HttpClient(
            //Use our custom Retry handler, with a max retry value of 10
            new RetryHandler(new HttpClientHandler(), 10))
        {
            MaxResponseContentBufferSize = (int)responseLength
        };

        //client.MaxResponseContentBufferSize = partSize;
        client.DefaultRequestHeaders.ConnectionClose = false;
        client.Timeout = Timeout.InfiniteTimeSpan;

        return client;
    });

    #endregion

    #region Helper Methods
    private static void SetMaxThreads()
    {

        ThreadPool.GetMaxThreads(out int maxworkerThreads,
                                 out int maxconcurrentActiveRequests);

        bool changeSucceeded = ThreadPool.SetMaxThreads(
                                   maxworkerThreads, maxconcurrentActiveRequests);
    }

    private static EventfulConcurrentQueue<FileChunk> GetTaskList(ProgressBar progress, double parts, Action<int> progressCallback = null)
    {
        var asyncTasks = new EventfulConcurrentQueue<FileChunk>();

        //Delegate for Dequeue
        asyncTasks.ItemDequeued += delegate
        {
            //Tasks done holds the count of the tasks done
            //Parts *2 because there are Parts number of Enqueue AND Dequeue operations
            if (progressCallback == null)
            {
                progress.Report(TasksDone / (parts * 2));
            }
            else
            {
                progressCallback?.Invoke(Convert.ToInt32(TasksDone / (parts * 2)));
            }
        };

        //Delegate for Enqueue
        asyncTasks.ItemEnqueued += delegate
        {
            if (progressCallback == null)
            {
                progress.Report(TasksDone / (parts * 2));
            }
            else
            {
                progressCallback?.Invoke(Convert.ToInt32(TasksDone / (parts * 2)));
            }
        };

        return asyncTasks;
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Tuple<Task<HttpResponseMessage>, FileChunk> getStreamTask(FileChunk piece, long responseLength, Uri uri, EventfulConcurrentQueue<FileChunk> asyncTasks)
    {
        Tuple<Task<HttpResponseMessage>, FileChunk> returnTuple;
        using (var wcObj = _wcPool.Get())
        {
            Console.Title = "STREAMING....";

            //Open a http request with the range
            var request = new HttpRequestMessage { RequestUri = uri };
            request.Headers.ConnectionClose = false;
            request.Headers.Range = new RangeHeaderValue(piece.Start, piece.End);

            //Send the request
            var downloadTask = wcObj.Value.SendAsync(request, HttpCompletionOption.ResponseContentRead, CancellationToken.None);

            //Use interlocked to increment Tasks done by one
            Interlocked.Add(ref TasksDone, 1);
            asyncTasks.Enqueue(piece);

            returnTuple = new Tuple<Task<HttpResponseMessage>, FileChunk>(downloadTask, piece);
        }

        return returnTuple;
    }

    private List<FileChunk> GetChunkList(long partSize, long responseLength)
    {
        //Variable to hold the old loop end
        var previous = 0;
        List<FileChunk> pieces = new List<FileChunk>();

        //Loop to add all the events to the queue
        for (var i = (int)partSize; i <= responseLength; i += (int)partSize)
        {
            Console.Title = "WRITING CHUNKS...";
            if (i + partSize < responseLength)
            {
                //Start and end values for the chunk
                var start = previous;
                var currentEnd = i;

                pieces.Add(new FileChunk(start, currentEnd, true));

                //Set the start of the next loop to be the current end
                previous = currentEnd;
            }
            else
            {
                //Start and end values for the chunk
                var start = previous;
                var currentEnd = i;

                pieces.Add(new FileChunk(start, (int)responseLength, true));

                //Set the start of the next loop to be the current end
                previous = currentEnd;
            }
        }

        return pieces;
    }
    #endregion

    #region Functions
    /// <summary>
    /// Download a resource as a byte array in memory
    /// </summary>
    /// <param name="URL">The URL of the resource to download.</param>
    /// <param name="parts">Number of parts to download file as</param>
    /// <param name="OnComplete">Completion OnComplete function</param>
    /// <param name="OnUpdate">Progress OnComplete function</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task DownloadByteArray(string URL, double parts, Action<byte[]> OnComplete,
                                        Action<int> OnUpdate = null)
    {
        var responseLength = (await WebRequest.Create(URL).GetResponseAsync()).ContentLength;
        var partSize = (long)Math.Floor(responseLength / parts);
        var pieces = new List<FileChunk>();

        ThreadPool.GetMaxThreads(out int maxworkerThreads,
                                 out int maxconcurrentActiveRequests);

        bool changeSucceeded = ThreadPool.SetMaxThreads(
                                   maxworkerThreads, maxconcurrentActiveRequests);
        Console.WriteLine(responseLength.ToString(CultureInfo.InvariantCulture) + " TOTAL SIZE");
        Console.WriteLine(partSize.ToString(CultureInfo.InvariantCulture) + " PART SIZE" + "\n");


        try
        {
            //Using our custom progressbar
            using (var progress = new ProgressBar())
            {
                using (var ms = new MemoryStream())
                {
                    ms.SetLength(responseLength);

                    //Using custom concurrent queue to implement Enqueue and Dequeue Events
                    var asyncTasks = new EventfulConcurrentQueue<FileChunk>();

                    //Delegate for Dequeue
                    asyncTasks.ItemDequeued += delegate
                    {
                        //Tasks done holds the count of the tasks done
                        //Parts *2 because there are Parts number of Enqueue AND Dequeue operations
                        if (OnUpdate == null)
                        {
                            progress.Report(TasksDone / (parts * 2));
                            Thread.Sleep(20);
                        }
                        else
                        {
                            OnUpdate?.Invoke(Convert.ToInt32(TasksDone / (parts * 2)));
                        }
                    };

                    //Delegate for Enqueue
                    asyncTasks.ItemEnqueued += delegate
                    {
                        if (OnUpdate == null)
                        {
                            progress.Report(TasksDone / (parts * 2));
                            Thread.Sleep(20);
                        }
                        else
                        {
                            OnUpdate?.Invoke(Convert.ToInt32(TasksDone / (parts * 2)));
                        }
                    };

                    // GetResponseAsync deadlocks for some reason so switched to HttpClient instead
                    var client = new HttpClient(
                        //Use our custom Retry handler, with a max retry value of 10
                        new RetryHandler(new HttpClientHandler(), 10))
                    {
                        MaxResponseContentBufferSize = 1000000000
                    };

                    client.DefaultRequestHeaders.ConnectionClose = false;
                    client.Timeout = Timeout.InfiniteTimeSpan;

                    //Variable to hold the old loop end
                    var previous = 0;

                    //Loop to add all the events to the queue
                    for (var i = (int)partSize; i <= responseLength; i += (int)partSize)
                    {
                        Console.Title = "WRITING CHUNKS...";
                        if (i + partSize < responseLength)
                        {
                            //Start and end values for the chunk
                            var start = previous;
                            var currentEnd = i;

                            pieces.Add(new FileChunk(start, currentEnd));

                            //Set the start of the next loop to be the current end
                            previous = currentEnd;
                        }
                        else
                        {
                            //Start and end values for the chunk
                            var start = previous;
                            var currentEnd = i;

                            pieces.Add(new FileChunk(start, (int)responseLength));

                            //Set the start of the next loop to be the current end
                            previous = currentEnd;
                        }
                    }

                    var getFileChunk = new TransformManyBlock<IEnumerable<FileChunk>, FileChunk>(chunk => chunk, new ExecutionDataflowBlockOptions()
                    {
                        BoundedCapacity = Int32.MaxValue, // Cap the item count
                        MaxDegreeOfParallelism = Environment.ProcessorCount, // Parallelize on all cores
                    });

                    var getStream = new TransformBlock<FileChunk, Tuple<Task<HttpResponseMessage>, FileChunk>>(
                        piece =>
                    {
                        Console.Title = "STREAMING....";
                        //Open a http request with the range
                        var request = new HttpRequestMessage { RequestUri = new Uri(URL) };
                        request.Headers.Range = new RangeHeaderValue(piece.Start, piece.End);

                        //Send the request
                        var downloadTask = client.SendAsync(request, HttpCompletionOption.ResponseContentRead);

                        //Use interlocked to increment Tasks done by one
                        Interlocked.Add(ref TasksDone, 1);
                        asyncTasks.Enqueue(piece);

                        return new Tuple<Task<HttpResponseMessage>, FileChunk>(downloadTask, piece);
                    }, new ExecutionDataflowBlockOptions
                    {
                        BoundedCapacity = (int)parts, // Cap the item count
                        MaxDegreeOfParallelism = Environment.ProcessorCount, // Parallelize on all cores
                    }
                    );

                    var writeStream = new ActionBlock<Tuple<Task<HttpResponseMessage>, FileChunk>>(async tuple =>
                    {
                        var buffer = new byte[tuple.Item2.End - tuple.Item2.Start];
                        using (var stream = await tuple.Item1.Result.Content.ReadAsStreamAsync())
                        {
                            await stream.ReadAsync(buffer, 0, buffer.Length);
                        }

                        lock (ms)
                        {
                            ms.Position = tuple.Item2.Start;
                            ms.Write(buffer, 0, buffer.Length);
                        }

                        var s = new FileChunk();
                        asyncTasks.TryDequeue(out s);
                        Interlocked.Add(ref TasksDone, 1);
                    }, new ExecutionDataflowBlockOptions
                    {
                        BoundedCapacity = (int)parts, // Cap the item count
                        MaxDegreeOfParallelism = Environment.ProcessorCount, // Parallelize on all cores
                    });

                    var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };

                    getFileChunk.LinkTo(getStream, linkOptions);
                    getStream.LinkTo(writeStream, linkOptions);

                    getFileChunk.Post(pieces);
                    getFileChunk.Complete();

                    await writeStream.Completion.ContinueWith(task =>
                    {
                        if (asyncTasks.Count == 0)
                        {
                            ms.Flush();
                            ms.Close();
                            OnComplete?.Invoke(ms.ToArray());
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="URL">The URL of the resource to download.</param>
    /// <param name="Parts">Number of parts to download file as</param>
    /// <param name="outFile">Outfile name, will be auto generated if null.</param>
    /// <param name="OnUpdate">Progress OnComplete function</param>
    /// <returns></returns>
    public async Task DownloadFile(string URL, double Parts, string outFile = null,
                                   Action<int> OnUpdate = null)
    {
        #region Variables
        EventfulConcurrentQueue<FileChunk> asyncTasks;
        TransformManyBlock<IEnumerable<FileChunk>, FileChunk> getFileChunk;
        TransformBlock<FileChunk, Tuple<Task<HttpResponseMessage>, FileChunk>> getStream;
        ActionBlock<Tuple<Task<HttpResponseMessage>, FileChunk>> writeStream;
        //Get response length
        responseLength = (await WebRequest.Create(URL).GetResponseAsync()).ContentLength;
        //Calculate Part size
        var partSize = (long)Math.Round(responseLength / Parts);
        //Get the content ranges to download
        var pieces = GetChunkList(partSize, responseLength);
        //Stores the default console title for later restore
        var defaultTitle = Console.Title;
        //URL To uri
        var uri = new Uri(URL);
        //Outfile name for later null check
        var filename = "";
        if (outFile == null)
        {
            filename = Path.GetFileName(uri.LocalPath);
        }
        else
        {
            filename = outFile;
        }
        #endregion

        Console.WriteLine(responseLength.ToString(CultureInfo.InvariantCulture) + " TOTAL SIZE");
        Console.WriteLine(partSize.ToString(CultureInfo.InvariantCulture) + " PART SIZE" + "\n");

        //Set max threads to those supported by system
        SetMaxThreads();

        try
        {
            using (var progress = new ProgressBar())
            {
                //Using custom concurrent queue to implement Enqueue and Dequeue Events
                asyncTasks = GetTaskList(progress, Parts, OnUpdate);

                Console.Title = "CHUNKS DONE";

                //Transform many to get from List<Filechunk> => Filechunk essentially iterating
                getFileChunk = new TransformManyBlock<IEnumerable<FileChunk>, FileChunk>(chunk => chunk, new ExecutionDataflowBlockOptions());

                //Gets the request stream from the filechunk
                getStream = new TransformBlock<FileChunk, Tuple<Task<HttpResponseMessage>, FileChunk>>(piece =>
                {
                    var newTask = getStreamTask(piece, responseLength, uri, asyncTasks);
                    return newTask;
                },
                new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = Environment.ProcessorCount,           // Cap the item count
                    MaxDegreeOfParallelism = Environment.ProcessorCount,    // Parallelize on all cores
                }
                                                                                                      );

                //Writes the request stream to a tempfile
                writeStream = new ActionBlock<Tuple<Task<HttpResponseMessage>, FileChunk>>(async task =>
                {
                    using (var streamToRead = await task.Item1.Result.Content.ReadAsStreamAsync())
                    {
                        using (var fileToWriteTo = File.Open(task.Item2.TempFileName, FileMode.OpenOrCreate,
                                                             FileAccess.ReadWrite, FileShare.ReadWrite))
                        {
                            fileToWriteTo.Position = 0;
                            await streamToRead.CopyToAsync(fileToWriteTo, (int)partSize, CancellationToken.None);
                        }
                        var s = new FileChunk();
                        Interlocked.Add(ref TasksDone, 1);
                        asyncTasks.TryDequeue(out s);
                    }
                    GC.Collect(0, GCCollectionMode.Forced);
                }, new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = Environment.ProcessorCount,           // Cap the item count
                    MaxDegreeOfParallelism = Environment.ProcessorCount,    // Parallelize on all cores
                });

                //Propage errors and completion
                var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };

                //Build the data flow pipeline
                getFileChunk.LinkTo(getStream, linkOptions);
                getStream.LinkTo(writeStream, linkOptions);

                //Post the file pieces
                getFileChunk.Post(pieces);
                getFileChunk.Complete();

                //Write all the streams
                await writeStream.Completion.ContinueWith(task =>
                {
                    //If all the tasks are done, Join the temp files
                    if (asyncTasks.Count == 0)
                    {
                        CombineMultipleFilesIntoSingleFile(pieces, filename);
                    }
                }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Current);

            }

            //Restore the original title
            Console.Title = defaultTitle;
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
                catch (FileNotFoundException) { }
            }
        }
    }
    #endregion
}
}
