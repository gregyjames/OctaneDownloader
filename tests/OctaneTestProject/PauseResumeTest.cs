using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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
    public class PauseResumeTest
    {
        private PauseTokenSource _pauseTokenSource;
        private CancellationTokenSource _cancelTokenSource;
        private ILogger _log;
        private ILoggerFactory _factory;
        private string _outFile;
        private byte[] _mockData;
        
        [SetUp]
        public void Init()
        {
            _outFile = Path.GetRandomFileName();
            _log = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Verbose()
                .WriteTo.Console()
                .CreateLogger();

            _factory = LoggerFactory.Create(logging =>
            {
                logging.AddSerilog(_log);
            });
            
            _pauseTokenSource = new PauseTokenSource(_factory);
            _cancelTokenSource = new CancellationTokenSource();

            _mockData = new byte[1024 * 50]; // 50 KB
            new Random().NextBytes(_mockData);
        }

        [TearDown]
        public void CleanUp()
        {
            try
            {
                if (File.Exists(_outFile))
                    File.Delete(_outFile);
            }
            catch
            {
                // ignored
            }
        }

        [Test]
        public void PauseResumeFile()
        {
            const string url = @"https://mockurl.com/file.png";

            _log.Information("Starting Pause Resume Test");
            
            using var mockClient = Helpers.GetMockHttpClient(_mockData);
            
            var config = new OctaneConfiguration
            {
                Parts = 2,
                BufferSize = 8192,
                ShowProgress = false,
                NumRetries = 20,
                BytesPerSecond = 1,
                UseProxy = false
            };
            
            _pauseTokenSource.Pause();
            
            var engine = new Engine(config, mockClient, _factory);
            
            var doneCalled = false;
            engine.SetDoneCallback(success =>
            {
                doneCalled = true;
                Assert.That(success, Is.True);
                Assert.That(File.Exists(_outFile), Is.True);
                Assert.That(File.ReadAllBytes(_outFile), Is.EqualTo(_mockData));
            });
            engine.SetProgressCallback(Console.WriteLine);
            engine.SetProxy(null);
            
            Parallel.Invoke(
                () => Action(_pauseTokenSource),
                () => engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait()
            );
            
            Assert.That(doneCalled, Is.True, "Done callback should have been invoked after resume");
        }

        private void Action(PauseTokenSource pcs)
        {
            Thread.Sleep(2000); // Reduced delay for faster test execution
            pcs.Resume();
        }
    }
}
