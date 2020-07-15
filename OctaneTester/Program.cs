using System;
using System.IO;
using System.Threading;
using OctaneDownloadEngine;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace OctaneDownloadEngine
{
    internal static class Program
    {
        private static void Main()
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load("files.xml");
            var files = xmlDoc.SelectNodes("//files/file");

            foreach (XmlNode fileNode in files)
            {
                var url = fileNode.Attributes["input"].Value;
                try
                {
                    var ODE = new OctaneEngine();
                    Console.Clear();
                    ODE.DownloadFile(url, 4).GetAwaiter().GetResult();
                    Thread.Sleep(1000);
                }
                catch
                {
                    var filename = Path.GetFileName(new Uri(url).LocalPath);
                    Console.WriteLine("Download error on " + filename);
                }
            }
        }
    }
}
