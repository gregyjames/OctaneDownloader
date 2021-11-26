using System.IO;
using NUnit.Framework;

namespace OctaneEngine
{
    [TestFixture]
    public class Tests
    {
        [SetUp]
        public void Init()
        {
            //_exists = false;
        }

        [TearDown]
        public void CleanUp()
        {
            //File.Delete("Chershire_Cat.24ee16b9.jpeg");
        }

        [Test]
        public void DownloadFile()
        {
            var t = Engine.DownloadFile("https://www.wonderland.money/static/media/Chershire_Cat.24ee16b9.jpeg", 4, "Chershire_Cat.24ee16b9.jpeg");
            t.Wait();
            if (t.IsCompletedSuccessfully)
            {
                Assert.IsTrue(File.Exists("Chershire_Cat.24ee16b9.jpeg"));
            }
        }
    }

}
