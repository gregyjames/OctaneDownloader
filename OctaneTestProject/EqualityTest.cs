using System.IO;
using System.Linq;
using System.Net;
using NUnit.Framework;
using OctaneEngine;

namespace OctaneTestProject
{
    public class EqualityTest
    {
        [SetUp]
        public void Init()
        {
            //_exists = false;
        }

        [TearDown]
        public void CleanUp()
        {
            File.Delete("1.jpeg");
            File.Delete("2.jpeg");
        }

        [Test]
        public void DownloadFile()
        {
            var url = "https://www.wonderland.money/static/media/Chershire_Cat.24ee16b9.jpeg";
            Engine.DownloadFile(url, 4, 256, false,"1.jpeg", b =>
            {
                using (var client = new WebClient())
                {
                    client.DownloadFile(url, "2.jpeg");
                }

                Assert.IsTrue(File.ReadAllBytes("1.jpeg").SequenceEqual(File.ReadAllBytes("2.jpeg")));
            }).Wait();
        }
    }
}