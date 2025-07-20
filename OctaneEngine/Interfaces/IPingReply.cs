using System.Net.NetworkInformation;

namespace OctaneEngineCore.Interfaces;

public interface IPingReply
{
    IPStatus Status { get; }
    long RoundtripTime { get; }
}