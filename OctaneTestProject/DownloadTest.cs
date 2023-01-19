using System;
using System.IO;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OctaneEngine;
using OctaneEngineCore;
using Serilog;

namespace OctaneTestProject
{
    [TestFixture]
    public class DownloadTest
    {
        private ILoggerFactory _factory = null;
        private PauseTokenSource _pauseTokenSource = null;
        
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
        }

        [TearDown]
        public void CleanUp()
        {
            File.Delete("Chershire_Cat.24ee16b9.png");
        }

        [Test]
        public void DownloadFile()
        {
            const string url = @"https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png";
            const string outFile = @"Chershire_Cat.24ee16b9.png";

            var config = new OctaneConfiguration
            {
                Parts = 2,
                BufferSize = 8192,
                ShowProgress = false,
                DoneCallback = x => Assert.IsTrue(File.Exists(outFile)),
                ProgressCallback = Console.WriteLine,
                NumRetries = 20,
                BytesPerSecond = 1,
                UseProxy = false,
                Proxy = null
            };
            
            Engine.DownloadFile(url, _factory, outFile, config, _pauseTokenSource).Wait();
        }
    }
}
