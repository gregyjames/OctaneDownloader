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
            SplitDownload("http://www.hdwallpapers.in/walls/tree_snake_hd-wide.jpg", "output.jpg");
            Console.ReadLine();

        }

        //Converts the Stream to a byte array
        public static byte[] ReadFully(Stream input)
        {
            var buffer = new byte[9000];
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

        static public void SplitDownload(string URL, string OUT)
        {
            var responseLength = WebRequest.Create(URL).GetResponse().ContentLength;
            var partSize = (long)Math.Floor(responseLength / 2.00);

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
                t.Priority = ThreadPriority.Highest;
                t.Start();
                previous = i2;
            }
        }

        static async private void Download(string URL, string OUT, int Start, int End, int partSize)
        {
                Console.WriteLine(String.Format("{0},{1}", Start, End));

                var myHttpWebRequest = (HttpWebRequest)WebRequest.Create(URL);
                myHttpWebRequest.AddRange(Start, End);
                myHttpWebRequest.Proxy = null;

                var stram = await myHttpWebRequest.GetResponseAsync();

                var array = ReadFully(stram.GetResponseStream());
                var fs = new FileStream(OUT, FileMode.Append, FileAccess.Write, FileShare.Write, partSize);
                fs.Position = Start;
                
                try
                {
                    foreach (byte x in array)
                    {
                        if (fs.Position != End)
                        {
                            fs.WriteByte(x);
                        }
                    }
                }
                catch { }
        }


    }
}
