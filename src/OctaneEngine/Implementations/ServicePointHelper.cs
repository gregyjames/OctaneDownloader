using System;
using System.Net;

namespace OctaneEngineCore.Implementations;

internal static class ServicePointHelper
{
    internal static void ConfigureServicePoint(int numParts)
    {
        ServicePointManager.Expect100Continue = false;
        ServicePointManager.DefaultConnectionLimit = 10000;
        ServicePointManager.SetTcpKeepAlive(true, Int32.MaxValue,1);
        ServicePointManager.MaxServicePoints = numParts;
        #if NET6_0_OR_GREATER
            ServicePointManager.ReusePort = true;
        #endif
    }
}