using System.Net;
using System.Net.Sockets;

namespace Dofus210.Host.Networking;

public static class NetworkEndpointResolver
{
    public static IPAddress ResolveListenAddress(string host)
    {
        if (string.IsNullOrWhiteSpace(host) ||
            host.Equals("0.0.0.0", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("any", StringComparison.OrdinalIgnoreCase))
        {
            return IPAddress.Any;
        }

        if (IPAddress.TryParse(host, out var ipAddress))
        {
            return ipAddress;
        }

        var addresses = Dns.GetHostAddresses(host);
        return addresses.First(address => address.AddressFamily == AddressFamily.InterNetwork);
    }
}
