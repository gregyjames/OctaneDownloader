using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;

namespace OctaneDownloadEngine
{
    class Program
    {
        static void Main()
        {
            SplitDownload("http://s.imgur.com/images/logo-1200-630.jpg", "output.jpg");
            Console.ReadLine();

        }

        //Function that makes the name for each part of the file that was downloaded
        static string GenerateName(int start)
        {
            String name = String.Format("{0:D6}.tmp", start);
            return name;
        }

        //Stores the names of all the temp files
        static public List<string> Files = new List<string>();

        //Merges all the Temp files
        static private void mergeClean(long partSize)
        {
            //Orders the array based on the name
            Files.Sort();
            using (var output = File.Create("output.jpg"))
            {
                Console.WriteLine("\n");
                foreach (var file in Files)
                {
                    Console.WriteLine(file);
                    using (var input = File.OpenRead(file))
                    {
                        var buffer = new byte[partSize];
                        int bytesRead;
                        while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            output.Write(buffer, 0, bytesRead);
                        }
                    }
                }
            }

            foreach (var file in Files)
            {
                File.Delete(file);
            }
        }

        //Converts the Stream to a byte array
        public static byte[] ReadFully(Stream input)
        {
            var buffer = new byte[16 * 1024];
            using (var ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

        //Save the file stream to a temp file
        static private void SaveFileStream(String path, Stream stream)
        {
            var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write);
            //var fileStream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);

            stream.CopyTo(fileStream);
            //fileStream.Write();
            fileStream.Dispose();
        }

        static public void SplitDownload(string URL, string OUT)
        {
            var responseLength = WebRequest.Create(URL).GetResponse().ContentLength;
            var partSize = (long)Math.Floor(responseLength / 3.00);

            Console.WriteLine(responseLength.ToString(CultureInfo.InvariantCulture) + " TOTAL SIZE");
            Console.WriteLine(partSize.ToString(CultureInfo.InvariantCulture) + " PART SIZE" + "\n");
            var previous = 0;

            FileStream fs = File.Create(OUT);
            fs.Close();
            for (var i = (int)partSize; i <= responseLength + partSize; i = (i + (int)partSize) + 1)
            {
                var previous2 = previous;
                var i2 = i;
                var t = new Thread(() => Download(URL, OUT, previous2, i2, (int)partSize));
                t.Start();
                previous = i;

            }




            mergeClean(partSize);
        }

        static private void Download(string URL, string OUT, int Start, int End, int partSize)
        {
            Console.WriteLine(String.Format("{0},{1}", Start, End));

            var myHttpWebRequest = (HttpWebRequest)WebRequest.Create(URL);
            myHttpWebRequest.AddRange(Start, End);
            var streamResponse = myHttpWebRequest.GetResponse().GetResponseStream();

            string name = GenerateName(Start);
            //String name = OUT;

            //var array = ReadFully(streamResponse);

            SaveFileStream(name, streamResponse);
            Files.Add(name);
            //Files.


        }


    }
}
