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
    public class FailureTest
    {
        private PauseTokenSource _pauseTokenSource;
        private CancellationTokenSource _cancelTokenSource;
        private ILogger _log;
        private ILoggerFactory _factory;
        readonly string _outFile = Path.GetRandomFileName();
        
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
                //File.Delete(outFile);
            }
            catch
            {
                // ignored
            }
        }

        [Test]
        public void TestFailure()
        {
            const string url = @"http://www.ooogle.com/w2a3h4a5a6s7h8a9j0d9696502025CM0987654321.jpg";

            _log.Information("Starting File Download Test");
            try
            {
                var config = new OctaneConfiguration
                {
                    Parts = 2,
                    BufferSize = 2048,
                    ShowProgress = false,
                    NumRetries = 1,
                    BytesPerSecond = 1,
                    UseProxy = false,
                };

                var engine = EngineBuilder.Create().WithConfiguration(config).WithLogger(_factory).Build();
                engine.SetProxy(null);
                engine.SetDoneCallback(status =>
                {
                    Assert.That(status, Is.False);
                });
                engine.SetProgressCallback(Console.WriteLine);
                engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource).Wait();
            }
            catch
            {
                // ignored
            }
        }
    }
}
