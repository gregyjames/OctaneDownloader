using System;
using System.IO;

namespace OctaneDownloadEngine
{
    public class FileChunk
    {
        public int Start { get; set; }
        public int End { get; set; }
        public string TempFileName  {get;}

        public FileChunk(){}
        public int Id { get; set; }

        public FileChunk(int startByte, int endByte)
        {
            TempFileName = Guid.NewGuid().ToString();
            Start = startByte;
            End = endByte;
        }

        public FileChunk(int startByte, int endByte, bool createFile)
        {
            TempFileName = Guid.NewGuid().ToString();
            File.Create(TempFileName);
            Start = startByte;
            End = endByte;
        }
    }
}
