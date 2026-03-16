using System.Threading.Tasks;

namespace OctaneEngineCore.Interfaces.NetworkAnalyzer;

public interface IPingService
{
    Task<IPingReply> SendPingAsync(string host);
}