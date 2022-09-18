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
            const string url = @"https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png";
            const string outFile = @"Chershire_Cat.24ee16b9.png";

            var config = new OctaneConfiguration
            {
                Parts = 4,
                ShowProgress = false,
                DoneCallback = x => Assert.IsTrue(File.Exists(outFile)),
                NumRetries = 20
            };
            
            Engine.DownloadFile(url, outFile, config).Wait();
        }
    }
}
