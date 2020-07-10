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
        public int id { get; set; }

        public FileChunk(int startByte, int endByte)
        {
            _tempfilename = Guid.NewGuid().ToString();
            File.Create(_tempfilename);
            start = startByte;
            end = endByte;
        }
    }
}
