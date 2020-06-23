using System;
using System.IO;

namespace OctaneDownloadEngine
{
    static class Program
    {
        static void Main(){
        OctaneEngine.SplitDownloadArray("http://www.hdwallpapers.in/walls/tree_snake_hd-wide.jpg", 8, "image.jpg", (x) => {
                File.WriteAllBytes("image.jpg", x);
                Console.WriteLine("Done!");
            });
            
            Console.ReadLine();
        }
    }
}