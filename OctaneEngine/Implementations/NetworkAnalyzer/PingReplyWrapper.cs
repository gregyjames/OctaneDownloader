using System.Diagnostics.CodeAnalysis;
using System.Net.NetworkInformation;
using OctaneEngineCore.Interfaces;
using OctaneEngineCore.Interfaces.NetworkAnalyzer;

namespace OctaneEngineCore.Implementations.NetworkAnalyzer;

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