using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace OctaneEngineCore.Interfaces;

public interface IPingService
{
    Task<IPingReply> SendPingAsync(string host);
}