namespace OctaneEngine
{
    internal readonly struct FileChunk
    {
        public long Start { get; }
        public long End { get; }

        public FileChunk(long startByte, long endByte)
        {
            this.Start = startByte;
            this.End = endByte;
        }
    }
}