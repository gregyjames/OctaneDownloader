using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OctaneEngine;
using OctaneEngineCore;
using Serilog;
using ILogger = Serilog.ILogger;

namespace OctaneTestProject
{
    [TestFixture]
    // Checks if pausing and resume during downloads works.
    public class PauseResumeTest
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
            //File.Delete(outFile);
        }

        [Test]
        public void PauseResumeFile()
        {
            const string url = @"https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png";

            _log.Information("Starting Pause Resume Test");
            
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
            
            var containerBuilder = new ContainerBuilder();
            containerBuilder.RegisterInstance(_factory).As<ILoggerFactory>();
            containerBuilder.RegisterInstance(config).As<OctaneConfiguration>();
            containerBuilder.AddOctane();
            var engineContainer = containerBuilder.Build();
            var engine = engineContainer.Resolve<IEngine>();

            engine.SetDoneCallback(_ => Assert.IsTrue(File.Exists(_outFile)));
            engine.SetProgressCallback(Console.WriteLine);
            engine.SetProxy(null);
            Parallel.Invoke(
                () => Action(_pauseTokenSource),
                () => engine.DownloadFile(url, _outFile, _pauseTokenSource, _cancelTokenSource).Wait()
            );
        }

        private void Action(PauseTokenSource pcs)
        {
            Thread.Sleep(5000);
            pcs.Resume();
        }
    }
}
