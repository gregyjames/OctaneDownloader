using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Threading;

// ReSharper disable All

namespace OctaneDownloadEngine
{
    public class OctaneEngine
    {
        protected OctaneEngine()
        {
            ServicePointManager.DefaultConnectionLimit = 10000;
        }

        public static async void SplitDownloadArray(string url, double parts, Action<byte[]> callback)
        {
            try
            {
                await DownloadByteArray(url, parts, callback).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                throw;
            }
        }

        public static void SplitDownloadArray(string[] urls, double parts, Action<byte[]> callback)
        {
            var tasks = new List<Task>();

            Parallel.ForEach(urls, url =>
            {
                tasks.Add(DownloadByteArray(url, parts, callback));
            });

            Task.WaitAll(tasks.ToArray());
        }

        static int TasksDone = 0;

        private static void CombineMultipleFilesIntoSingleFile(string inputDirectoryPath, IEnumerable<FileChunk> files, string outputFilePath)
        {
            var _files = files.Select(chunk => chunk._tempfilename).ToArray();
            
            Console.WriteLine("Number of files: {0}.", _files.Length);
            using (var outputStream = File.Create(outputFilePath))
            {
                foreach (var inputFilePath in _files)
                {
                    using (var inputStream = new BinaryWriter(File.OpenWrite(inputFilePath)))
                    {
                        // Buffer size can be passed as the second argument.
                        //inputStream.CopyTo(outputStream, files.First().end - files.First().start);
                        var s = File.Open(inputFilePath, FileMode.Open);
                        var bytes = new byte[s.Length];
                        var buffer = s.Read(bytes , 0, files.First().end-files.First().start);
                        inputStream.Write(buffer);
                    }
                    Console.WriteLine("The file {0} has been processed.", inputFilePath);
                    //File.Delete(inputFilePath);
                }
            }
        }

        private async static Task DownloadByteArray(string url, double parts, Action<byte[]> callback, Action<int> progressCallback = null)
        {
            var responseLength = (await WebRequest.Create(url).GetResponseAsync()).ContentLength;
            var partSize = (long)Math.Floor(responseLength / parts);
            var pieces = new List<FileChunk>();

            Console.WriteLine(responseLength.ToString(CultureInfo.InvariantCulture) + " TOTAL SIZE");
            Console.WriteLine(partSize.ToString(CultureInfo.InvariantCulture) + " PART SIZE" + "\n");

            var ms = new MemoryStream();
            try
            {
                //Using our custom progressbar
                using (var progress = new ProgressBar())
                {
                    ms.SetLength(responseLength);

                    //Using custom concurrent queue to implement Enqueue and Dequeue Events
                    var asyncTasks = new EventfulConcurrentQueue<Tuple<Task<Stream>, FileChunk>>();

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

                    // GetResponseAsync deadlocks for some reason so switched to HttpClient instead
                    var client = new HttpClient(
                        //Use our custom Retry handler, with a max retry value of 10
                        new RetryHandler(new HttpClientHandler(), 10)) {MaxResponseContentBufferSize = 1000000000};

                    //Variable to hold the old loop end
                    var previous = 0;

                    //Loop to add all the events to the queue
                    for (var i = (int) partSize; i < responseLength; i += (int) partSize)
                    {
                        //Start and end values for the chunk
                        var start = previous;
                        var current_end = i;

                        pieces.Add(new FileChunk(start, current_end));

                        //Set the start of the next loop to be the current end
                        previous = current_end;
                    }

                    Parallel.ForEach(pieces, piece =>
                    {
                        //Open a http request with the range
                        var request = new HttpRequestMessage { RequestUri = new Uri(url) };
                        request.Headers.Range = new RangeHeaderValue(piece.start, piece.end);

                        //Send the request
                        var downloadTask = client.SendAsync(request).Result;

                        //Use interlocked to increment Tasks done by one
                        Interlocked.Add(ref OctaneEngine.TasksDone, 1);

                        //Add the task to the queue along with the start and end value
                        asyncTasks.Enqueue(
                            new Tuple<Task<Stream>, FileChunk>(downloadTask.Content.ReadAsStreamAsync(),
                                piece));
                    });
                    // now that all the downloads are started, we can await the results
                    // loop through looking for a completed task in case they complete out of order
                    while (asyncTasks.Count > 0)
                    {
                        Parallel.ForEach(asyncTasks.Queue, async (task, state) =>
                        {
                            // as each task completes write the data to the file
                            //if (task.Item1.IsCompleted)
                            //{
                                //var array = await task.Item1.ConfigureAwait(false);
                                //lock (ms)
                                //{

                                using (FileStream fs = new FileStream(task.Item2._tempfilename, FileMode.OpenOrCreate, FileAccess.Write))
                                {
                                    //lock (fs) 
                                    //{ 
                                        task.Item1.Result.CopyToAsync(fs).Wait();
                                    //}
                                }

                                asyncTasks.TryDequeue(out task);
                                Interlocked.Add(ref TasksDone, 1);
                                //}

                            //}
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                CombineMultipleFilesIntoSingleFile(Directory.GetCurrentDirectory(), pieces, "image2.jpg");
                //ms.Flush();
                //ms.Close();
                //callback?.Invoke(ms.ToArray());
            }
        }

        private static void CombineMultipleFilesIntoSingleFile(string inputDirectoryPath, List<FileChunk> files, string outputFilePath)
        {
            Console.WriteLine("Number of files: {0}.", files.Count);
            using (var outputStream = File.Create(outputFilePath))
            {
                foreach (var inputFilePath in files)
                {
                    using (var inputStream = File.OpenRead(inputFilePath._tempfilename))
                    {
                        // Buffer size can be passed as the second argument.
                        outputStream.Position = inputFilePath.start;
                        inputStream.CopyTo(outputStream);
                    }
                    Console.WriteLine("The file has been processed from {0} to {1}.", inputFilePath.start, inputFilePath.end);
                    File.Delete(inputFilePath._tempfilename);
                }
            }
        }

        public async static Task DownloadFile(string url, double parts, string outFile, Action<int> progressCallback = null)
        {
            var responseLength = (await WebRequest.Create(url).GetResponseAsync()).ContentLength;
            var partSize = (long)Math.Round(responseLength / parts);
            var pieces = new List<FileChunk>();

            Console.WriteLine(responseLength.ToString(CultureInfo.InvariantCulture) + " TOTAL SIZE");
            Console.WriteLine(partSize.ToString(CultureInfo.InvariantCulture) + " PART SIZE" + "\n");

            try
            {
                //Using our custom progressbar
                using (var progress = new ProgressBar())
                {
                    //Using custom concurrent queue to implement Enqueue and Dequeue Events
                    var asyncTasks = new EventfulConcurrentQueue<Tuple<Task, FileChunk>>();

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

                    // GetResponseAsync deadlocks for some reason so switched to HttpClient instead
                    var client = new HttpClient(
                        //Use our custom Retry handler, with a max retry value of 10
                        new RetryHandler(new HttpClientHandler(), 10))
                    { MaxResponseContentBufferSize = 1000000000 };

                    //Variable to hold the old loop end
                    var previous = 0;

                    //Loop to add all the events to the queue
                    for (var i = (int)partSize; i < responseLength; i += (int)partSize)
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

                            pieces.Add(new FileChunk(start, (int)responseLength));

                            //Set the start of the next loop to be the current end
                            previous = current_end;
                        }
                    }

                    Parallel.ForEach(pieces, piece =>
                    {
                        //Open a http request with the range
                        var request = new HttpRequestMessage { RequestUri = new Uri(url) };
                        request.Headers.Range = new RangeHeaderValue(piece.start, piece.end);

                        //Send the request
                        var downloadTask = client.SendAsync(request).Result;

                        //Use interlocked to increment Tasks done by one
                        Interlocked.Add(ref OctaneEngine.TasksDone, 1);

                        //Add the task to the queue along with the start and end value
                        asyncTasks.Enqueue(new Tuple<Task, FileChunk>(downloadTask.Content.ReadAsStreamAsync().ContinueWith(
                            task =>
                            {
                                using (var fs = new FileStream(piece._tempfilename, FileMode.OpenOrCreate,
                                    FileAccess.Write))
                                {
                                    task.Result.CopyTo(fs);
                                }
                            }), piece));
                    });

                    while (asyncTasks.Count > 0)
                    {
                        Parallel.ForEach(asyncTasks.Queue, async (task, state) =>
                        {
                            task.Item1.Wait();
                            asyncTasks.TryDequeue(out task);
                            Interlocked.Add(ref TasksDone, 1);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                CombineMultipleFilesIntoSingleFile(Directory.GetCurrentDirectory(), pieces, outFile);
            }
        }
    }
}