using System.Diagnostics.CodeAnalysis;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using OctaneEngineCore.Interfaces;

namespace OctaneEngineCore.Implementations;

[ExcludeFromCodeCoverage]
public class PingService : IPingService
{
    public async Task<IPingReply> SendPingAsync(string host)
    {
        using var ping = new Ping();
        var reply = await ping.SendPingAsync(host);
        return new PingReplyWrapper(reply);
    }
}