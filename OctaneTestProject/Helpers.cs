using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using Serilog;
using ILogger = Serilog.ILogger;

namespace OctaneTestProject
{
    public static class Helpers
    {
        public static bool AreFilesEqual(string file1, string file2)
        {
            int attempts = 0;
            const int maxAttempts = 10;
            const int waitMilliseconds = 2000;

            while (attempts < maxAttempts)
            {
                try
                {
                    using (FileStream stream1 = new FileStream(file1, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (FileStream stream2 = new FileStream(file2, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        byte[] file1Bytes = new byte[stream1.Length];
                        byte[] file2Bytes = new byte[stream2.Length];

                        stream1.Read(file1Bytes, 0, file1Bytes.Length);
                        stream2.Read(file2Bytes, 0, file2Bytes.Length);

                        if (file1Bytes.Length != file2Bytes.Length)
                        {
                            return false;
                        }

                        for (int i = 0; i < file1Bytes.Length; i++)
                        {
                            if (file1Bytes[i] != file2Bytes[i])
                            {
                                return false;
                            }
                        }

                        return true;
                    }
                }
                catch (IOException)
                {
                    // The file is currently locked by another process, wait for a bit and try again
                    Thread.Sleep(waitMilliseconds);
                    attempts++;
                }
            }

            throw new Exception($"Failed to read files {file1} and {file2} after {maxAttempts} attempts.");
        }
    }
}