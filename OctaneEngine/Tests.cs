using System.IO;
using NUnit.Framework;
using OctaneEngine;

namespace OctaneEngine
{
    [TestFixture]
    public class Tests
    {
        private bool exists;

        [SetUp]
        public void Init()
        {
            exists = false;
        }

        [TearDown]
        public void CleanUp()
        {
            //File.Delete("Chershire_Cat.24ee16b9.jpeg");
        }

        [Test]
        public void DownloadFile()
        {
            var t = Engine.DownloadFile("https://www.wonderland.money/static/media/Chershire_Cat.24ee16b9.jpeg", 4,
                "Chershire_Cat.24ee16b9.jpeg");
            t.Wait();
            if (t.IsCompletedSuccessfully)
            {
                Assert.IsTrue(File.Exists("Chershire_Cat.24ee16b9.jpeg"));
            }
        }
    }

}
