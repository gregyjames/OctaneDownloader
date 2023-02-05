using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OctaneEngine;
using OctaneEngineCore;
using Serilog;

namespace OctaneTestProject
{
    [TestFixture]
    // Checks if the octane downloaded file is equal to download the file with HTTPClient.
    public class EqualityTest
    {
        private ILoggerFactory _factory;
        private PauseTokenSource _pauseTokenSource;
        private CancellationTokenSource _cancelTokenSource;

        [SetUp]
        public void Init()
        {
            var seriLog = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Verbose()
                .WriteTo.File("./OctaneLog.txt")
                .WriteTo.Console()
                .CreateLogger();

            _factory = LoggerFactory.Create(logging =>
            {
                logging.AddSerilog(seriLog);
            });

            _pauseTokenSource = new PauseTokenSource(_factory);
            _cancelTokenSource = new CancellationTokenSource();
        }

        [TearDown]
        public void CleanUp()
        {
            File.Delete("Chershire_Cat.24ee16b9.png");
        }

        private static bool AreFilesEqual(string file1, string file2)
        {
            byte[] file1Bytes = File.ReadAllBytes(file1);
            byte[] file2Bytes = File.ReadAllBytes(file2);

            if (file1Bytes.Length != file2Bytes.Length)
            {
                return false;
            }

            for (int i = 0; i < file1Bytes.Length; i++)
            {
                if (file1Bytes[i] != file2Bytes[i])
                {
                    return false;
                }
            }

            return true;
        }
        
        [Test]
        public void DownloadFile()
        {
            const string url = @"https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png";
            const string outFile = @"Chershire_Cat.24ee16b9.png";

            var client = new HttpClient();
            var response = client.GetAsync(url).Result;
            var stream = response.Content.ReadAsStreamAsync().Result;

            var fileStream = File.Create("original.png");
            stream.CopyTo(fileStream);
            fileStream.Close();

            var done = false;
            var config = new OctaneConfiguration
            {
                Parts = 2,
                BufferSize = 8192,
                ShowProgress = false,
                DoneCallback = _ => done = true,
                ProgressCallback = Console.WriteLine,
                NumRetries = 20,
                BytesPerSecond = 1,
                UseProxy = false,
                Proxy = null
            };

            Task download = null;
            if (File.Exists("original.png"))
            {
                download = Engine.DownloadFile(url, _factory, outFile, config, _pauseTokenSource, _cancelTokenSource);
                download.Wait();
            }

            if (File.Exists(outFile) && done)
            {
                AreFilesEqual("original.png", outFile);
            }
        }
    }
}