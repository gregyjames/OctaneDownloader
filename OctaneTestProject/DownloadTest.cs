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
            File.Delete("Chershire_Cat.24ee16b9.png");
        }

        [Test]
        public void DownloadFile()
        {
            var url = "https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png";
            var outFile = "Chershire_Cat.24ee16b9.png";
            
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
