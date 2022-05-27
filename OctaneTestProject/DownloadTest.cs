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
            var url = "https://file-examples.com/storage/fe2a923118628f9c0986d27/2017/10/file_example_JPG_2500kB.jpg";
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
