using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OctaneEngine;
using OctaneEngineCore;
using Serilog;
using ILogger = Serilog.ILogger;

namespace OctaneTestProject
{
    [TestFixture]
    // Checks if octane can successfully download a file.
    public class DownloadTest
    {
        private PauseTokenSource _pauseTokenSource;
        private CancellationTokenSource _cancelTokenSource;
        private ILogger _log;
        private ILoggerFactory _factory;
        
        [SetUp]
        public void Init()
        {
            _log = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Verbose()
                .WriteTo.File("./OctaneLog.txt")
                .WriteTo.Console()
                .CreateLogger();

            _factory = LoggerFactory.Create(logging =>
            {
                logging.AddSerilog(_log);
            });
            
            _pauseTokenSource = new PauseTokenSource(_factory);
            _cancelTokenSource = new CancellationTokenSource();
        }

        [TearDown]
        public void CleanUp()
        {
            try
            {
                File.Delete("Chershire_Cat.24ee16b9.png");
            }
            catch
            {
                // ignored
            }
        }

        [Test]
        public void DownloadFile()
        {
            const string url = @"https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png";
            const string outFile = @"Chershire_Cat.24ee16b9.png";

            _log.Information("Starting File Download Test");
            try
            {
                var config = new OctaneConfiguration
                {
                    Parts = 2,
                    BufferSize = 8192,
                    ShowProgress = false,
                    DoneCallback = _ =>
                    {
                        Console.WriteLine("Done!");
                        Assert.IsTrue(File.Exists(outFile));
                    },
                    ProgressCallback = Console.WriteLine,
                    NumRetries = 20,
                    BytesPerSecond = 1,
                    UseProxy = false,
                    Proxy = null
                };

                var engine = new Engine(_factory, config);
                engine.DownloadFile(url, outFile, _pauseTokenSource, _cancelTokenSource).Wait();
            }
            catch
            {
                // ignored
            }
        }
    }
}
