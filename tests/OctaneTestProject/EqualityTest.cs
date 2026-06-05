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
    public class EqualityTest
    {
        private PauseTokenSource _pauseTokenSource;
        private CancellationTokenSource _cancelTokenSource;
        private ILogger _log;
        private ILoggerFactory _factory;
        private byte[] _mockData;
        private string _originalFile;
        private string _outFile;
        
        [SetUp]
        public void Init()
        {
            _originalFile = Path.GetRandomFileName();
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

            // Set up random mock bytes
            _mockData = new byte[1024 * 50]; // 50 KB
            new Random().NextBytes(_mockData);
        }

        [TearDown]
        public void CleanUp()
        {
            try
            {
                if (File.Exists(_originalFile))
                    File.Delete(_originalFile);
                if (File.Exists(_outFile))
                    File.Delete(_outFile);
            }
            catch
            {
                // ignored
            }
        }

        [Test]
        public void FileEqualityTest()
        {
            const string url = @"https://mockurl.com/file.png";
            
            _log.Information("Starting File Equality Test");
            var done = false;
            
            using var mockHandler = new MockHttpMessageHandler(_mockData);
            using var mockClient = new HttpClient(mockHandler);
            
            // Step 1: Simulate live download using custom HttpMessageHandler to write original file
            var response = mockClient.GetAsync(url).Result;
            using (var stream = response.Content.ReadAsStreamAsync().Result)
            {
                using (var fileStream = File.Create(_originalFile))
                {
                    stream.CopyTo(fileStream);
                }
            }
            
            var config = new OctaneConfiguration
            {
                Parts = 2,
                BufferSize = 1024,
                ShowProgress = false,
                NumRetries = 20,
                BytesPerSecond = 1,
                UseProxy = false,
            };

            if (File.Exists(_originalFile))
            {
                var engine = new Engine(config, mockClient, _factory);
                
                engine.SetDoneCallback(_ => done = true);
                engine.SetProgressCallback(Console.WriteLine);
                engine.SetProxy(null);
                    
                var t = engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token);
                t.Wait();
            }
            
            bool equal = true;
            if (File.Exists(_outFile) && done)
            {
                byte[] file1Bytes = File.ReadAllBytes(_originalFile);
                byte[] file2Bytes = File.ReadAllBytes(_outFile);

                if (file1Bytes.Length != file2Bytes.Length)
                {
                    equal = false;
                    _log.Error("Files are different lengths: {} vs {}", file1Bytes.Length, file2Bytes.Length);
                }

                for (int i = 0; i < file1Bytes.Length; i++)
                {
                    if (file1Bytes[i] != file2Bytes[i])
                    {
                        _log.Error("Files are different at {location} {a} vs {b}", i, file1Bytes[i], file2Bytes[i]);
                        equal = false;
                    }
                }
            }
            else
            {
                equal = false;
            }
            
            Assert.That(equal, Is.True, "Downloaded file must match the original content exactly");
        }
    }
}