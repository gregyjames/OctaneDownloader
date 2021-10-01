using System;
using OctaneDownloadEngine;
namespace OctaneDownloadEngine
{
    internal static class Program
    {
        private static void Main()
        {
            var s = "";
            while (s != "EXIT")
            {
                Console.Write("Enter file URL: ");
                s = Console.ReadLine();

                OctaneEngine.DownloadFile(s, 4).ContinueWith(x =>
                {
                    Console.WriteLine("DONE!");
                }).Wait();
            }
            Console.ReadLine();

        }
    }
}