using System;
using System.IO;

namespace OctaneDownloadEngine
{
    class FileChunk
    {
        public int start { get; set; }
        public int end { get; set; }

        public string _tempfilename = "";

        public FileChunk(){}

        public FileChunk(int startByte, int endByte)
        {
            _tempfilename = Guid.NewGuid().ToString();
            start = startByte;
            end = endByte;
        }
    }
}
