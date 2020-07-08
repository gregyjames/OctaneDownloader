using System;
using System.IO;

namespace OctaneDownloadEngine
{
    static class Program
    {
        
        static void Main(){
        
        OctaneEngine.SplitDownloadArray("http://www.nasa.gov/sites/default/files/thumbnails/image/hs-2015-02-a-hires_jpg.jpg", 8, (x) => {
                Console.WriteLine("Writing to file...");
                File.WriteAllBytes("image.jpg", x);
                Console.WriteLine("DONE!");
            });
            
            Console.ReadLine();
        }
    }
}