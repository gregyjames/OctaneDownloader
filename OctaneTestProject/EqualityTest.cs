﻿using System;
using System.IO;
using System.Net.Http;
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
                    BufferSize = 8192,
                    ShowProgress = false,
                    DoneCallback = _ => done = true,
                    ProgressCallback = Console.WriteLine,
                    NumRetries = 20,
                    BytesPerSecond = 1,
                    UseProxy = false,
                    Proxy = null
                };

                if (File.Exists("original.png"))
                {
                    var containerBuilder = new ContainerBuilder();
                    containerBuilder.RegisterInstance(_factory).As<ILoggerFactory>();
                    containerBuilder.RegisterInstance(config).As<OctaneConfiguration>();
                    containerBuilder.AddOctane();
                    var engineContainer = containerBuilder.Build();
                    var engine = engineContainer.Resolve<IEngine>();
                    var t = engine.DownloadFile(url, outFile, _pauseTokenSource, _cancelTokenSource);
                    t.Wait();
                }
            }
            catch(Exception ex)
            {
                _log.Error(ex.Message);
            }
            finally
            {
                bool equal = false;
                if (File.Exists(outFile) && done)
                {
                    byte[] file1Bytes = File.ReadAllBytes("original.png");
                    byte[] file2Bytes = File.ReadAllBytes(outFile);

                    if (file1Bytes.Length != file2Bytes.Length)
                    {
                        equal = false;
                    }

                    for (int i = 0; i < file1Bytes.Length; i++)
                    {
                        if (file1Bytes[i] != file2Bytes[i])
                        {
                            equal = false;
                            break;
                        }

                        equal = true;
                    }
                }
                Assert.IsTrue(equal);
            }
        }
    }
}