using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
    public class HandlingDownloadCancellation
    {
        private const string BigLocalFile = "bigfilename.txt";
        private const string BigFileUrl = "https://ash-speed.hetzner.com/1GB.bin";
        
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
            _cancelTokenSource.CancelAfter(TimeSpan.FromSeconds(5));
        }

        [TearDown]
        public void CleanUp()
        {
            try
            {
                File.Delete(BigLocalFile);
            }
            catch
            {
                // ignored
            }
        }

        [Test]
        public void DownloadFile()
        {
            _log.Information("Starting File Download Test");
            var bigStatus = true;
            
            try
            {
                var bigFileDownloader = EngineBuilder.Build(_factory, new OctaneConfiguration()
                {
                    Parts = 4
                });
        
                bigFileDownloader.SetDoneCallback( status =>
                {
                    Console.WriteLine(status
                        ? "Done callback big file called"
                        : "Error Done callback big file called");
                    bigStatus = status;
                });
                
                var tasks = new List<Task>
                {
                    bigFileDownloader.DownloadFile(BigFileUrl, BigLocalFile, _pauseTokenSource, _cancelTokenSource)
                        .ContinueWith(_ => Console.WriteLine("Done download big file continuation")),
                };

                Task.WaitAll(tasks.ToArray());
                Console.WriteLine("Download all files finished!");
                
                
                Assert.That(File.Exists(BigLocalFile), Is.False);
                Assert.That(bigStatus, Is.False);
            }
            catch
            {
                // ignored
            }
        }
    }
}

