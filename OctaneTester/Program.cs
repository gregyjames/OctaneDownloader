using System;
using System.IO;

namespace OctaneDownloadEngine
{
    static class Program
    {
        
<<<<<<< HEAD
        static void Main()
        {
            var downloadFile = OctaneEngine.DownloadFile(
                "http://212.183.159.230/20MB.zip", 10, "file.zip");

            downloadFile.Wait();

=======
        static void Main(){
        
        OctaneEngine.SplitDownloadArray("https://prd-wret.s3.us-west-2.amazonaws.com/assets/palladium/production/s3fs-public/thumbnails/image/test-5mb.png", 2, (x) => {
                Console.WriteLine("Writing to file...");
                File.WriteAllBytes("image.png", x);
                Console.WriteLine("DONE!");
            });
            
>>>>>>> Tempfiles
            Console.ReadLine();
        }
    }
}