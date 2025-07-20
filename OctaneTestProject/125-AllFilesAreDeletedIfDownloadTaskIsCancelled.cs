using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OctaneEngine;
using Serilog;
using ILogger = Serilog.ILogger;

/*
namespace OctaneTestProject
{
    [TestFixture]
    // Checks if octane can successfully download a file.
    public class AllFilesAreDeletedIfDownloadTaskIsCancelledTest
    {
        private const string BigLocalFile = "bigfilename.txt";
        private const string BigFileUrl = "https://ash-speed.hetzner.com/10GB.bin";
        private const string SmallLocalFile = "smallfilename.txt";
        private const string SmallFileUrl = "https://freetestdata.com/wp-content/uploads/2021/09/1-MB-DOC.doc";
        
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
                //File.Delete(SmallLocalFile);
                //File.Delete(BigLocalFile);
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
            var bigFile = true;
            var smallFile = false;
            
            try
            {
                var bigFileDownloader = EngineBuilder.Build(_factory, new OctaneConfiguration()
                {
                    Parts = 1,
                    BufferSize = 512
                });
        
                bigFileDownloader.SetDoneCallback( status =>
                {
                    bigFile = status;
                    Console.WriteLine(status
                        ? "Done callback big file called"
                        : "Error Done callback big file called");
                });
        
                var smallFileDownloader = EngineBuilder.Build(_factory, new OctaneConfiguration()
                {
                    Parts = 1
                });
        
                smallFileDownloader.SetDoneCallback(status =>
                {
                    smallFile = status;
                    Console.WriteLine(status
                        ? "Done callback small file called"
                        : "Error Done callback small file called");
                });

                var tasks = new List<Task>
                {
                    bigFileDownloader.DownloadFile(BigFileUrl, BigLocalFile, _pauseTokenSource, _cancelTokenSource)
                        .ContinueWith(_ => Console.WriteLine("Done download big file continuation")),
                    smallFileDownloader.DownloadFile(SmallFileUrl, SmallLocalFile, _pauseTokenSource, _cancelTokenSource)
                        .ContinueWith(_ => Console.WriteLine("Done download small file continuation"))
                };

                Task.WaitAll(tasks.ToArray());
                Console.WriteLine("Download all files finished!");
                
                Assert.That(File.Exists(BigLocalFile), Is.False);
                Assert.That(File.Exists(SmallLocalFile), Is.True);
                Assert.That(smallFile, Is.True);
                Assert.That(bigFile, Is.False);
            }
            catch
            {
                // ignored
            }
        }
    }
}
*/