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

        var capacity = end - start + 1;
        if (count > capacity)
        {
            throw new ContainerMcpException(
                McpErrorCode.InvalidArgument,
                "count cannot exceed the requested port range capacity.",
                StatusCodes.Status400BadRequest,
                details: new JsonObject
                {
                    ["start"] = start,
                    ["end"] = end,
                    ["count"] = count,
                    ["capacity"] = capacity
                });
        }

        if (!protocol.Equals("tcp", StringComparison.OrdinalIgnoreCase) && !protocol.Equals("udp", StringComparison.OrdinalIgnoreCase))
        {
            throw new ContainerMcpException(McpErrorCode.InvalidArgument, "protocol must be tcp or udp.", StatusCodes.Status400BadRequest);
        }

        var busy = protocol.Equals("tcp", StringComparison.OrdinalIgnoreCase) ? BusyTcpPorts() : BusyUdpPorts();
        var ports = new List<int>(count);
        var busyPorts = new List<int>();
        var bindFailedPorts = new List<int>();
        var scanned = 0;
        for (var port = start; port <= end && ports.Count < count; port++)
        {
            scanned++;
            if (busy.Contains(port))
            {
                busyPorts.Add(port);
                continue;
            }

            if (CanBind(host, port, protocol))
            {
                ports.Add(port);
            }
            else
            {
                bindFailedPorts.Add(port);
            }
        }

        if (ports.Count < count)
        {
            throw new ContainerMcpException(
                McpErrorCode.PortRangeExhausted,
                "No free port was found in the requested range.",
                StatusCodes.Status409Conflict,
                details: BuildDiagnostics(host, start, end, count, protocol, scanned, ports, busyPorts, bindFailedPorts));
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

    private static JsonObject BuildDiagnostics(
        string host,
        int start,
        int end,
        int count,
        string protocol,
        int scanned,
        IReadOnlyCollection<int> foundPorts,
        IReadOnlyCollection<int> busyPorts,
        IReadOnlyCollection<int> bindFailedPorts) => new()
    {
        ["host"] = host,
        ["start"] = start,
        ["end"] = end,
        ["count"] = count,
        ["protocol"] = protocol.ToLowerInvariant(),
        ["scanned"] = scanned,
        ["found"] = foundPorts.Count,
        ["foundPorts"] = IntArray(foundPorts),
        ["busyPorts"] = IntArray(busyPorts),
        ["bindFailedPorts"] = IntArray(bindFailedPorts)
    };

    private static JsonArray IntArray(IEnumerable<int> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.AddNode(JsonValue.Create(value));
        }

        return array;
    }

    private static string? ReadString(JsonElement args, string name) => ToolArgumentReader.OptionalString(args, name);
    private static int? ReadInt(JsonElement args, string name) => ToolArgumentReader.OptionalInt(args, name);
}
