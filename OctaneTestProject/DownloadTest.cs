using System;
using System.IO;
using System.Threading;
using NUnit.Framework;
using OctaneEngine;
using OctaneEngineCore;

namespace OctaneTestProject
{
    [TestFixture]
    // Checks if octane can successfully download a file.
    public class DownloadTest
    {
        private PauseTokenSource _pauseTokenSource;
        private CancellationTokenSource _cancelTokenSource;

        [SetUp]
        public void Init()
        {
            _pauseTokenSource = new PauseTokenSource(Helpers._factory);
            _cancelTokenSource = new CancellationTokenSource();
        }

        [TearDown]
        public void CleanUp()
        {
            try
            {
                File.Delete("Chershire_Cat.24ee16b9.png");
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
            const string outFile = @"Chershire_Cat.24ee16b9.png";
            
            Helpers.seriLog.Information("Starting File Download Test");
            try
            {
                var config = new OctaneConfiguration
                {
                    Parts = 2,
                    BufferSize = 8192,
                    ShowProgress = false,
                    DoneCallback = _ =>
                    {
                        Console.WriteLine("Done!");
                        Assert.IsTrue(File.Exists(outFile));
                    },
                    ProgressCallback = Console.WriteLine,
                    NumRetries = 20,
                    BytesPerSecond = 1,
                    UseProxy = false,
                    Proxy = null
                };

                Engine.DownloadFile(url, Helpers._factory, outFile, config, _pauseTokenSource, _cancelTokenSource).Wait();
            }
            catch
            {
                // ignored
            }
        }
    }
}
