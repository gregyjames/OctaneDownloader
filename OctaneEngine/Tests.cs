using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace OctaneDownloadEngine
{
    [TestFixture]
    public class Tests
    {
        private OctaneEngine _engine;

        [SetUp]
        public void Init()
        {
            _engine = new OctaneEngine();
        }

        [TearDown]
        public void CleanUp()
        {
            File.Delete("test_img.png");
        }

        [Test]
        public async Task DownloadFile()
        {
            await _engine.DownloadFile(
                "https://upload.wikimedia.org/wikipedia/commons/thumb/b/b6/Image_created_with_a_mobile_phone.png/1200px-Image_created_with_a_mobile_phone.png",
                4, "test_img.png").ContinueWith((tas) =>
            {
                Assert.IsTrue(File.Exists("test_img.png"));
            }, CancellationToken.None,TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Current);
        }
    }
}
