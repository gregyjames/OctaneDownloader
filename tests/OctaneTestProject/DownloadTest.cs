using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OctaneEngineCore;
using OctaneEngineCore.Clients;
using OctaneEngineCore.Implementations;
using Serilog;
using ILogger = Serilog.ILogger;

namespace OctaneTestProject
{
    [TestFixture]
    public class DownloadTest
    {
        private PauseTokenSource _pauseTokenSource;
        private CancellationTokenSource _cancelTokenSource;
        private ILogger _log;
        private ILoggerFactory _factory;
        private string _outFile;
        
        [SetUp]
        public void Init()
        {
            _outFile = Path.GetRandomFileName();
            _log = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Verbose()
                .WriteTo.File($"./{_outFile}.log")
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
                if (File.Exists(_outFile))
                    File.Delete(_outFile);
                
                var logFile = $"./{_outFile}.log";
                if (File.Exists(logFile))
                    File.Delete(logFile);
            }
            catch
            {
                // ignored
            }
        }

        [Test]
        public void DownloadFile()
        {
            const string url = @"https://mockurl.com/file.png";
            
            // Generate mock file content
            var mockData = new byte[1024 * 100]; // 100 KB
            new Random().NextBytes(mockData);
            
            using var mockHandler = new MockHttpMessageHandler(mockData);
            using var mockClient = new HttpClient(mockHandler);

            _log.Information("Starting File Download Test");
            
            var config = new OctaneConfiguration
            {
                Parts = 2,
                BufferSize = 8192,
                ShowProgress = false,
                NumRetries = 20,
                BytesPerSecond = 1,
                UseProxy = false,
            };

            var engine = new Engine(config, mockClient, _factory);
            
            engine.SetProxy(null);
            
            var doneCalled = false;
            engine.SetDoneCallback(success =>
            {
                Console.WriteLine("Done!");
                doneCalled = true;
                Assert.That(success, Is.True);
                Assert.That(File.Exists(_outFile), Is.True);
                
                var downloadedData = File.ReadAllBytes(_outFile);
                Assert.That(downloadedData, Is.EqualTo(mockData));
            });
            
            engine.SetProgressCallback(Console.WriteLine);
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            
            Assert.That(doneCalled, Is.True, "Done callback should be invoked");
        }
    }
}
