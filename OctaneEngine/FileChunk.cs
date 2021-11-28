using System;

namespace OctaneEngine
{
    internal class FileChunk
    {
        public long Start { get; set; }
        public long End { get; set; }
        public FileChunk(){}
        public int Id { get; set; }

        public FileChunk(long startByte, long endByte)
        {
            Start = startByte;
            End = endByte;
        }
    }
}
