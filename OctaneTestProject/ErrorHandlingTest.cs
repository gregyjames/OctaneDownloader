using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OctaneEngine;
using OctaneEngineCore;
using OctaneEngineCore.Clients;
using Serilog;
using ILogger = Serilog.ILogger;

namespace OctaneTestProject
{
    [TestFixture]
    public class ErrorHandlingTest
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
                if (File.Exists(_outFile))
                    File.Delete(_outFile);
            }
            catch
            {
                // ignored
            }
        }

        [Test]
        public void ErrorHandling_InvalidUrl_ShouldThrowException()
        {
            const string invalidUrl = @"00invalid-url-that-does-not-exist.com/file.zip";

            _log.Information("Testing error handling with invalid URL");
            
            var engine = EngineBuilder.Create(config =>
            {
                config.Parts = 2;
                config.BufferSize = 8192;
                config.ShowProgress = false;
                config.NumRetries = 1; // Low retries for faster failure
                config.BytesPerSecond = 1;
                config.UseProxy = false;
                config.LowMemoryMode = false;
            }, _factory).Build();
            
            Assert.That(engine, Is.Not.Null);
            
            var exceptionThrown = false;
            
            try
            {
                engine.DownloadFile(new OctaneRequest(invalidUrl, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            }
            catch (Exception)
            {
                exceptionThrown = true;
            }
            
            Assert.That(exceptionThrown, Is.True, "Should throw exception for invalid URL");
        }

        [Test]
        public void ErrorHandling_NonExistentFile_ShouldThrowException()
        {
            const string nonExistentUrl = @"https://httpbin.org/status/404";

            _log.Information("Testing error handling with non-existent file");
            
            var engine = EngineBuilder.Create(config =>
            {
                config.Parts = 2;
                config.BufferSize = 8192;
                config.ShowProgress = false;
                config.NumRetries = 1; // Low retries for faster failure
                config.BytesPerSecond = 1;
                config.UseProxy = false;
                config.LowMemoryMode = false;
            }, _factory).Build();
            
            Assert.That(engine, Is.Not.Null);
            
            var exceptionThrown = false;
            
            try
            {
                engine.DownloadFile(new OctaneRequest(nonExistentUrl, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            }
            catch (Exception)
            {
                exceptionThrown = true;
            }
            
            Assert.That(exceptionThrown, Is.True, "Should throw exception for non-existent file");
        }

        [Test]
        public void ErrorHandling_Cancellation_ShouldCancelDownload()
        {
            const string url = @"https://httpbin.org/delay/10"; // 10 second delay

            _log.Information("Testing error handling with cancellation");
            
            var engine = EngineBuilder.Create(config =>
            {
                config.Parts = 2;
                config.BufferSize = 8192;
                config.ShowProgress = false;
                config.NumRetries = 3;
                config.BytesPerSecond = 1;
                config.UseProxy = false;
                config.LowMemoryMode = false;
            }, _factory).Build();
            
            Assert.That(engine, Is.Not.Null);
            
            var cancellationRequested = false;
            
            // Cancel after 2 seconds
            var timer = new System.Threading.Timer(_ =>
            {
                _cancelTokenSource.Cancel();
                cancellationRequested = true;
            }, null, 2000, Timeout.Infinite);
            
            var exceptionThrown = false;
            
            try
            {
                engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            }
            catch (OperationCanceledException)
            {
                exceptionThrown = true;
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
            {
                exceptionThrown = true;
            }
            
            timer.Dispose();
            
            Assert.That(cancellationRequested, Is.True, "Cancellation should be requested");
            Assert.That(exceptionThrown, Is.True, "Should throw cancellation exception");
        }

        [Test]
        public void ErrorHandling_ZeroParts_ShouldUseDefault()
        {
            const string url = @"https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png";

            _log.Information("Testing error handling with zero parts");
            
            var engine = EngineBuilder.Create(config =>
            {
                config.Parts = 0; // Invalid value
                config.BufferSize = 8192;
                config.ShowProgress = false;
                config.NumRetries = 3;
                config.BytesPerSecond = 1;
                config.UseProxy = false;
                config.LowMemoryMode = false;
            }, _factory).Build();
            
            Assert.That(engine, Is.Not.Null);
            
            engine.SetDoneCallback(_ =>
            {
                Console.WriteLine("Done with zero parts!");
                Assert.That(File.Exists(_outFile), Is.True);
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
        }

        [Test]
        public void ErrorHandling_ZeroBufferSize_ShouldUseDefault()
        {
            const string url = @"https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png";

            _log.Information("Testing error handling with zero buffer size");
            
            var engine = EngineBuilder.Create(config =>
            {
                config.Parts = 2;
                config.BufferSize = 0; // Invalid value
                config.ShowProgress = false;
                config.NumRetries = 3;
                config.BytesPerSecond = 1;
                config.UseProxy = false;
                config.LowMemoryMode = false;
            }, _factory).Build();
            
            Assert.That(engine, Is.Not.Null);
            
            engine.SetDoneCallback(_ =>
            {
                Console.WriteLine("Done with zero buffer size!");
                Assert.That(File.Exists(_outFile), Is.True);
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
        }

        [Test]
        public void ErrorHandling_InvalidConfiguration_ShouldHandleGracefully()
        {
            const string url = @"https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png";

            _log.Information("Testing error handling with invalid configuration");
            
            var engine = EngineBuilder.Create(config =>
            {
                config.Parts = -1; // Invalid negative value
                config.BufferSize = -1; // Invalid negative value
                config.ShowProgress = false;
                config.NumRetries = -1; // Invalid negative value
                config.BytesPerSecond = -1; // Invalid negative value
                config.UseProxy = false;
                config.LowMemoryMode = false;
            }, _factory).Build();
            
            Assert.That(engine, Is.Not.Null);
            
            engine.SetDoneCallback(_ =>
            {
                Console.WriteLine("Done with invalid configuration!");
                Assert.That(File.Exists(_outFile), Is.True);
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
        }
    }
} 