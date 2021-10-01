using System;
using System.IO;

namespace OctaneDownloadEngine
{
    internal class FileChunk
    {
        public int Start { get; set; }
        public int End { get; set; }
        public string TempFileName = "";

        public FileChunk(){}
        public int Id { get; set; }

        public FileChunk(int startByte, int endByte)
        {
            TempFileName = Guid.NewGuid().ToString();
            File.Create(TempFileName);
            Start = startByte;
            End = endByte;
        }
    }
}
