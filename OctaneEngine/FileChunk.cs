using System;
<<<<<<< HEAD
=======
using System.IO;
>>>>>>> Tempfiles

namespace OctaneDownloadEngine
{
    class FileChunk
    {
        public int start { get; set; }
        public int end { get; set; }

<<<<<<< HEAD
        public string _tempfilename;
=======
        public string _tempfilename = "";
>>>>>>> Tempfiles

        public FileChunk(){}

        public FileChunk(int startByte, int endByte)
        {
            _tempfilename = Guid.NewGuid().ToString();
            start = startByte;
            end = endByte;
            _tempfilename = Guid.NewGuid().ToString();
        }
    }
}
