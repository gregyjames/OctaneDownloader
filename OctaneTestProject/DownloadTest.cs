using System.IO;
using NUnit.Framework;
using OctaneEngine;

namespace OctaneTestProject
{
    [TestFixture]
    public class DownloadTest
    {
        [SetUp]
        public void Init()
        {
            
        }

        [TearDown]
        public void CleanUp()
        {
            File.Delete("Chershire_Cat.24ee16b9.jpeg");
        }

        [Test]
        public void DownloadFile()
        {
            var url = "https://www.wonderland.money/static/media/Chershire_Cat.24ee16b9.jpeg";
            var outFile = "Chershire_Cat.24ee16b9.jpeg";
            
            Engine.DownloadFile(url, 4, 256,
                false,outFile, b =>
                {
                    if (b)
                    {
                        Assert.IsTrue(File.Exists(outFile));
                    }
                }).Wait();
        }
    }
}