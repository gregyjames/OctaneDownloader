using System.IO;
using NUnit.Framework;
using OctaneEngine;

namespace OctaneTestProject {
[TestFixture]
public class DownloadTest {
  [SetUp]
  public void Init() {}

  [TearDown]
  public void CleanUp() { File.Delete("Chershire_Cat.24ee16b9.jpeg"); }

  [Test]
  public void DownloadFile() {
    var url =
        "https://upload.wikimedia.org/wikipedia/commons/thumb/f/f4/Nintendo-Famicom-Disk-System.png/1024px-Nintendo-Famicom-Disk-System.png";
    var outFile = "Chershire_Cat.24ee16b9.jpeg";

    Engine
        .DownloadFile(url, 4, 256, false, outFile,
                      b => {
                        if (b) {
                          Assert.IsTrue(File.Exists(outFile));
                        }
                      })
        .Wait();
  }
}
}
