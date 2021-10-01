using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OctaneDownloadEngine;
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
        public void DownloadFile()
        {
            OctaneEngine.DownloadFile("https://download.visualstudio.microsoft.com/download/pr/aa5eedba-8906-4e2b-96f8-1b4f06187460/e6757becd35f67b0897bcdda44baec93/dotnet-sdk-5.0.401-win-x64.exe", 4, "setup.exe").Wait();
            Assert.IsTrue(File.Exists("setup.exe"));
        }
    }

}
