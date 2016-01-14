using System;
using OctaneDownloadEngine;

namespace OctaneDownloadEngine
{
    class Program
    {
        static void Main()
        {
            var Engine = new OctaneEngine();
            Engine.SplitDownload("http://www.hdwallpapers.in/walls/tree_snake_hd-wide.jpg", "output.jpg", 4);

            
            Console.ReadLine();
        }
    }
}