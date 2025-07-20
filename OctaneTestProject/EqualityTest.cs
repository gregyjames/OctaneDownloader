using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OctaneEngine;
using OctaneEngine.Clients;
using Serilog;
using ILogger = Serilog.ILogger;

namespace OctaneTestProject
{
    [TestFixture]
    // Checks if the octane downloaded file is equal to download the file with HTTPClient.
    public class EqualityTest
    {
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
        }

        [TearDown]
        public void CleanUp()
        {
            //File.Delete("Chershire_Cat.24ee16b9.png");
        }

        [Test]
        public void FileEqualityTest()
        {
            const string url = @"https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png";
            string outFile = Path.GetRandomFileName();
            
            _log.Information("Starting File Equality Test");
            var done = false;
            
            try
            {
                using (var client = new HttpClient())
                {
                    var response = client.GetAsync(url).Result;
                    using (var stream = response.Content.ReadAsStreamAsync().Result)
                    {
                        using (var fileStream = File.Create("original.png"))
                        {
                            stream.CopyTo(fileStream);
                        }
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

                if (File.Exists("original.png"))
                {
                    var engine = EngineBuilder.Create().WithConfiguration(config).WithLogger(_factory).Build();
                    engine.SetDoneCallback(_ => done = true);
                    engine.SetProgressCallback(Console.WriteLine);
                    engine.SetProxy(null);
                        
                    var t = engine.DownloadFile(new OctaneRequest(url, outFile), _pauseTokenSource, _cancelTokenSource.Token);
                    t.Wait();
                }
            }
            catch(Exception ex)
            {
                _log.Error(ex.Message);
            }
            finally
            {
                bool equal = true;
                if (File.Exists(outFile) && done)
                {
                    byte[] file1Bytes = File.ReadAllBytes("original.png");
                    byte[] file2Bytes = File.ReadAllBytes(outFile);

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
                        else
                        {
                            equal = true;
                        }
                    }
                }
                Assert.That(equal, Is.True);
            }
        }
    }
}