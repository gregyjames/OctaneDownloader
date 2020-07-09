using System;
using System.IO;

namespace OctaneDownloadEngine
{
    static class Program
    {
        
        static void Main()
        {
            var downloadFile = OctaneEngine.DownloadFile(
                "http://212.183.159.230/20MB.zip", 10, "file.zip");

            downloadFile.Wait();

            Console.ReadLine();
        }
    }
}