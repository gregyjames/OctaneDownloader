

/*
namespace OctaneTestProject
{
    [TestFixture]
    // Checks if octane can successfully download a file.
    public class HandlingDownloadCancellation
    {
        private const string BigLocalFile = "bigfilename.txt";
        private const string BigFileUrl = "https://ash-speed.hetzner.com/10GB.bin";
        
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
            _cancelTokenSource.CancelAfter(TimeSpan.FromSeconds(3));
        }

        [TearDown]
        public void CleanUp()
        {
            try
            {
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
            var bigStatus = true;
            
            try
            {
                var bigFileDownloader = EngineBuilder.Build(_factory, new OctaneConfiguration()
                {
                    Parts = 1
                });
        
                bigFileDownloader.SetDoneCallback( status =>
                {
                    Console.WriteLine(status
                        ? "Done callback big file called"
                        : "Error Done callback big file called");
                    bigStatus = status;
                });

                bigFileDownloader.DownloadFile(BigFileUrl, BigLocalFile, _pauseTokenSource, _cancelTokenSource).Wait();

                Console.WriteLine("Download all files finished!");
                
                
                //Assert.That(File.Exists(BigLocalFile), Is.False);
                Assert.That(bigStatus, Is.False);
            }
            catch
            {
                // ignored
            }
        }
    }
}
*/
