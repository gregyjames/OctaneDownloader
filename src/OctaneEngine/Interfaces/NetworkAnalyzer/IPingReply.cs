using System.Net.NetworkInformation;

namespace OctaneEngineCore.Interfaces.NetworkAnalyzer;

public interface IPingReply
{
    IPStatus Status { get; }
    long RoundtripTime { get; }
}