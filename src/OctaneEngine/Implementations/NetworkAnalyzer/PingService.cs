using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using OctaneEngineCore.Interfaces;
using OctaneEngineCore.Interfaces.NetworkAnalyzer;

namespace OctaneEngineCore.Implementations.NetworkAnalyzer;

[ExcludeFromCodeCoverage]
public class PingService : IPingService
{
    public async Task<IPingReply> SendPingAsync(string host, CancellationToken cancellationToken = default)
    {
        using var ping = new Ping();
#if NET7_0_OR_GREATER
        var reply = await ping.SendPingAsync(host, TimeSpan.FromSeconds(5), null, null, cancellationToken).ConfigureAwait(false);
#else
        var reply = await ping.SendPingAsync(host).ConfigureAwait(false);
#endif
        return new PingReplyWrapper(reply);
    }
}