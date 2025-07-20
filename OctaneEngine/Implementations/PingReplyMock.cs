using System.Net.NetworkInformation;
using OctaneEngineCore.Interfaces;

namespace OctaneEngineCore.Implementations;

public class PingReplyMock : IPingReply
{
    public IPStatus Status { get; set; }
    public long RoundtripTime { get; set; }

    public PingReplyMock(IPStatus status, long roundtripTime)
    {
        Status = status;
        RoundtripTime = roundtripTime;
    }
}