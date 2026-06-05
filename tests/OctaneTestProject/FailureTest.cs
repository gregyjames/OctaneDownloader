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
    public class FailureTest
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
        public void TestFailure()
        {
            const string url = @"https://invalid-domain-name.com/file.jpg";

            _log.Information("Starting Mocked Failure Test");
            
            // Set up mock client in failure mode
            using var mockClient = Helpers.GetMockHttpClient(Array.Empty<byte>(), shouldFail: true);
            
            var config = new OctaneConfiguration
            {
                Parts = 2,
                BufferSize = 2048,
                ShowProgress = false,
                NumRetries = 1,
                BytesPerSecond = 1,
                UseProxy = false,
            };

            var engine = new Engine(config, mockClient, _factory);
            
            engine.SetProxy(null);
            
            var doneCalled = false;
            engine.SetDoneCallback(success =>
            {
                doneCalled = true;
                Assert.That(success, Is.False, "Done callback should report success = false on request failure");
            });
            
            engine.SetProgressCallback(Console.WriteLine);
            
            // We expect DownloadFile to throw an exception on network failure
            Assert.ThrowsAsync<HttpRequestException>(new NUnit.Framework.AsyncTestDelegate(async () =>
            {
                await engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token);
            }));
            
            Assert.That(doneCalled, Is.True, "Done callback should still be invoked on failure");
        }
    }
}
