namespace OctaneTestProject
{
    /*
    [TestFixture]
    // Checks if canceling a download works.
    public class CancelTest
    {
        private ILoggerFactory _factory;
        private PauseTokenSource _pauseTokenSource;
        private CancellationTokenSource _cancelTokenSource;
        
        [SetUp]
        public void Init()
        {
            var seriLog = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Verbose()
                .WriteTo.File("./OctaneLog.txt")
                .WriteTo.Console()
                .CreateLogger();

            _factory = LoggerFactory.Create(logging =>
            {
                logging.AddSerilog(seriLog);
            });

            _pauseTokenSource = new PauseTokenSource(_factory);
            _cancelTokenSource = new CancellationTokenSource();
        }

        [TearDown]
        public void CleanUp()
        {
            File.Delete("Chershire_Cat.24ee16b9.png");
        }

        [Test]
        public void DownloadFile()
        {
            const string url = @"https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png";
            const string outFile = @"Chershire_Cat.24ee16b9.png";
            
            var client = new HttpClient();
            var response = client.GetAsync(url).Result;
            var stream = response.Content.ReadAsStreamAsync().Result;

            var fileStream = File.Create("original.png");
            stream.CopyTo(fileStream);
            fileStream.Close();

            if (File.Exists("original.png"))
            {
                var config = new OctaneConfiguration
                {
                    Parts = 2,
                    BufferSize = 8192,
                    ShowProgress = false,
                    DoneCallback = c => Assert.IsTrue(!File.Exists(outFile) || !c || !Helpers.AreFilesEqual(outFile, "original.png")),
                    ProgressCallback = Console.WriteLine,
                    NumRetries = 20,
                    BytesPerSecond = 1,
                    UseProxy = false,
                    Proxy = null
                };

                Parallel.Invoke(
                    () => _cancelTokenSource.Cancel(),
                    () => Action(url, _factory, outFile, config, _pauseTokenSource, _cancelTokenSource).Wait()
                );
            }
        }

        private async Task Action(string url, ILoggerFactory factory, string outfile, OctaneConfiguration config, PauseTokenSource pauseTokenSource, CancellationTokenSource cancelTokenSource)
        {
            await Task.Delay(5000);
            await Engine.DownloadFile(url, factory, outfile, config, pauseTokenSource, cancelTokenSource);
        }
    }
    */
}
