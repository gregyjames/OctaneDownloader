using System.Diagnostics.CodeAnalysis;
using System.Net.NetworkInformation;
using OctaneEngineCore.Interfaces;

namespace OctaneEngineCore.Implementations;

[ExcludeFromCodeCoverage]
public class PingReplyWrapper : IPingReply
{
    private readonly PingReply _reply;

    public PingReplyWrapper(PingReply reply)
    {
        _reply = reply;
    }

    public IPStatus Status => _reply.Status;
    public long RoundtripTime => _reply.RoundtripTime;
}