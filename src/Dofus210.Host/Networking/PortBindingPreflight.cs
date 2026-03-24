using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using Dofus210.Host.Options;

namespace Dofus210.Host.Networking;

public static class PortBindingPreflight
{
    public static void ThrowIfPortsUnavailable(ServerOptions serverOptions)
    {
        var listenAddress = NetworkEndpointResolver.ResolveListenAddress(serverOptions.Host);
        var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
        var ownership = CaptureWindowsTcpOwnership();

        var requestedEndpoints = new[]
        {
            new RequestedEndpoint("Auth listener", "Server:AuthPort", listenAddress, serverOptions.AuthPort),
            new RequestedEndpoint("Game listener", "Server:GamePort", listenAddress, serverOptions.GamePort)
        };

        if (serverOptions.EnableSocketPolicyServer)
        {
            requestedEndpoints =
            [
                .. requestedEndpoints,
                new RequestedEndpoint("Socket policy listener", "Server:SocketPolicyPort", listenAddress, serverOptions.SocketPolicyPort)
            ];
        }

        var conflicts = requestedEndpoints
            .SelectMany(requestedEndpoint => listeners
                .Where(listener => listener.Port == requestedEndpoint.Port)
                .Where(listener => EndpointsConflict(requestedEndpoint.Address, listener.Address))
                .Select(listener => CreateConflict(requestedEndpoint, listener, ownership)))
            .ToList();

        if (conflicts.Count == 0)
        {
            return;
        }

        var lines = new List<string>
        {
            "Port preflight failed. The requested listener endpoints are already in use:"
        };

        foreach (var conflict in conflicts)
        {
            var ownerSuffix = conflict.ProcessId is null
                ? string.Empty
                : conflict.ProcessName is null
                    ? $" by PID {conflict.ProcessId}"
                    : $" by PID {conflict.ProcessId} ({conflict.ProcessName})";

            lines.Add(
                $"- {conflict.ListenerName} {FormatEndpoint(conflict.RequestedAddress, conflict.Port)} conflicts with existing {FormatEndpoint(conflict.OccupiedAddress, conflict.Port)}{ownerSuffix}. Configure {conflict.ConfigurationKey} if you want a different port.");
        }

        lines.Add("Fix the environment:");
        lines.Add("- Stop the process already listening on that port.");
        lines.Add("- Change the matching Server:*Port value in appsettings.json.");

        throw new InvalidOperationException(string.Join(Environment.NewLine, lines));
    }

    private static PortConflict CreateConflict(
        RequestedEndpoint requestedEndpoint,
        IPEndPoint occupiedEndpoint,
        IReadOnlyList<TcpOwnershipEntry> ownership)
    {
        var owner = ownership.FirstOrDefault(entry =>
            entry.Port == occupiedEndpoint.Port &&
            EndpointsConflict(entry.Address, occupiedEndpoint.Address));

        return new PortConflict(
            requestedEndpoint.Name,
            requestedEndpoint.ConfigurationKey,
            requestedEndpoint.Address,
            occupiedEndpoint.Address,
            requestedEndpoint.Port,
            owner?.ProcessId,
            owner?.ProcessName);
    }

    private static bool EndpointsConflict(IPAddress requestedAddress, IPAddress occupiedAddress)
    {
        return requestedAddress.Equals(occupiedAddress) ||
               requestedAddress.Equals(IPAddress.Any) ||
               occupiedAddress.Equals(IPAddress.Any);
    }

    private static string FormatEndpoint(IPAddress address, int port)
    {
        return $"{address}:{port}";
    }

    private static IReadOnlyList<TcpOwnershipEntry> CaptureWindowsTcpOwnership()
    {
        if (!OperatingSystem.IsWindows())
        {
            return Array.Empty<TcpOwnershipEntry>();
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netstat",
                    Arguments = "-ano -p tcp",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(2000);

            return output
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ParseOwnershipEntry)
                .Where(entry => entry is not null)
                .Cast<TcpOwnershipEntry>()
                .ToList();
        }
        catch
        {
            return Array.Empty<TcpOwnershipEntry>();
        }
    }

    private static TcpOwnershipEntry? ParseOwnershipEntry(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length < 5 ||
            !parts[0].Equals("TCP", StringComparison.OrdinalIgnoreCase) ||
            !parts[3].Equals("LISTENING", StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(parts[4], out var processId) ||
            !TryParseNetstatEndpoint(parts[1], out var endpoint))
        {
            return null;
        }

        string? processName = null;

        try
        {
            processName = Process.GetProcessById(processId).ProcessName;
        }
        catch
        {
        }

        return new TcpOwnershipEntry(endpoint.Address, endpoint.Port, processId, processName);
    }

    private static bool TryParseNetstatEndpoint(string token, out IPEndPoint endpoint)
    {
        endpoint = default!;

        var separatorIndex = token.LastIndexOf(':');

        if (separatorIndex <= 0 || separatorIndex == token.Length - 1)
        {
            return false;
        }

        var addressToken = token[..separatorIndex].Trim('[', ']');
        var portToken = token[(separatorIndex + 1)..];

        if (!int.TryParse(portToken, out var port))
        {
            return false;
        }

        if (addressToken == "*" || addressToken == "0.0.0.0")
        {
            endpoint = new IPEndPoint(IPAddress.Any, port);
            return true;
        }

        if (!IPAddress.TryParse(addressToken, out var address))
        {
            return false;
        }

        endpoint = new IPEndPoint(address, port);
        return true;
    }

    private sealed record RequestedEndpoint(string Name, string ConfigurationKey, IPAddress Address, int Port);

    private sealed record PortConflict(
        string ListenerName,
        string ConfigurationKey,
        IPAddress RequestedAddress,
        IPAddress OccupiedAddress,
        int Port,
        int? ProcessId,
        string? ProcessName);

    private sealed record TcpOwnershipEntry(IPAddress Address, int Port, int ProcessId, string? ProcessName);
}
