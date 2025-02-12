using System;
using System.IO;
using System.Threading;
using Autofac;
using Microsoft.Extensions.Configuration;
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
        public void DownloadFile()
        {
            const string url = @"https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png";

            _log.Information("Starting File Download Test");
            try
            {
                var config = new OctaneConfiguration
                {
                    Parts = 2,
                    BufferSize = 8192,
                    ShowProgress = false,
                    NumRetries = 20,
                    BytesPerSecond = 1,
                    UseProxy = false,
                };

                var containerBuilder = new ContainerBuilder();
                containerBuilder.RegisterInstance(_factory).As<ILoggerFactory>();
                containerBuilder.RegisterInstance(config).As<OctaneConfiguration>();
                containerBuilder.AddOctane();
                var engineContainer = containerBuilder.Build();
                var engine = engineContainer.Resolve<IEngine>();
                engine.SetProxy(null);
                engine.SetDoneCallback(_ =>
                {
                    Console.WriteLine("Done!");
                    Assert.That(File.Exists(_outFile), Is.True);
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
