using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using ContainerMcp.Models;
using ContainerMcp.Tools;

namespace ContainerMcp.Ports;

internal sealed class PortDiscoveryService
{
    public JsonObject FindFree(JsonElement args)
    {
        var host = ReadString(args, "host") ?? "127.0.0.1";
        var start = ReadInt(args, "start") ?? 1024;
        var end = ReadInt(args, "end") ?? 65535;
        var count = ReadInt(args, "count") ?? 1;
        var protocol = ReadString(args, "protocol") ?? "tcp";

        if (start < 1 || end > 65535 || start > end || count < 1)
        {
            throw new ContainerMcpException(McpErrorCode.InvalidArgument, "Invalid port discovery range.", StatusCodes.Status400BadRequest);
        }

        if (!protocol.Equals("tcp", StringComparison.OrdinalIgnoreCase) && !protocol.Equals("udp", StringComparison.OrdinalIgnoreCase))
        {
            throw new ContainerMcpException(McpErrorCode.InvalidArgument, "protocol must be tcp or udp.", StatusCodes.Status400BadRequest);
        }

        var busy = protocol.Equals("tcp", StringComparison.OrdinalIgnoreCase) ? BusyTcpPorts() : BusyUdpPorts();
        var ports = new List<int>(count);
        for (var port = start; port <= end && ports.Count < count; port++)
        {
            if (busy.Contains(port))
            {
                continue;
            }

            if (CanBind(host, port, protocol))
            {
                ports.Add(port);
            }
        }

        if (ports.Count < count)
        {
            throw new ContainerMcpException(McpErrorCode.PortRangeExhausted, "No free port was found in the requested range.", StatusCodes.Status409Conflict);
        }

        var items = new JsonArray();
        foreach (var port in ports)
        {
            items.AddNode(new JsonObject
            {
                ["host"] = host,
                ["port"] = port,
                ["protocol"] = protocol.ToLowerInvariant()
            });
        }

        return new JsonObject
        {
            ["engine"] = "none",
            ["target"] = "local",
            ["items"] = items
        };
    }

    private static HashSet<int> BusyTcpPorts()
    {
        var properties = IPGlobalProperties.GetIPGlobalProperties();
        return properties.GetActiveTcpListeners().Select(endpoint => endpoint.Port)
            .Concat(properties.GetActiveTcpConnections().Select(connection => connection.LocalEndPoint.Port))
            .ToHashSet();
    }

    private static HashSet<int> BusyUdpPorts() =>
        IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners().Select(endpoint => endpoint.Port).ToHashSet();

    private static bool CanBind(string host, int port, string protocol)
    {
        if (!IPAddress.TryParse(host, out var address))
        {
            address = IPAddress.Loopback;
        }

        try
        {
            if (protocol.Equals("tcp", StringComparison.OrdinalIgnoreCase))
            {
                var listener = new TcpListener(address, port);
                listener.Start();
                listener.Stop();
                return true;
            }

            using var udp = new UdpClient(new IPEndPoint(address, port));
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static string? ReadString(JsonElement args, string name) => ToolArgumentReader.OptionalString(args, name);
    private static int? ReadInt(JsonElement args, string name) => ToolArgumentReader.OptionalInt(args, name);
}
