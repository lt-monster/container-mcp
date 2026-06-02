using System.Text.Json.Nodes;
using ContainerMcp.Models;

namespace ContainerMcp.Tools;

internal static class ContainerToolRequests
{
    private static readonly string[] WaitConditions = ["not-running", "next-exit", "removed"];

    public static string BuildPausePath(string idOrName) =>
        $"/containers/{Uri.EscapeDataString(idOrName)}/pause";

    public static string BuildUnpausePath(string idOrName) =>
        $"/containers/{Uri.EscapeDataString(idOrName)}/unpause";

    public static string BuildRenamePath(string idOrName, string name) =>
        $"/containers/{Uri.EscapeDataString(idOrName)}/rename?name={Uri.EscapeDataString(name)}";

    public static string BuildStatsPath(string idOrName) =>
        $"/containers/{Uri.EscapeDataString(idOrName)}/stats?stream=false";

    public static string BuildTopPath(string idOrName, string? psArgs)
    {
        var path = $"/containers/{Uri.EscapeDataString(idOrName)}/top";
        return string.IsNullOrWhiteSpace(psArgs) ? path : path + "?ps_args=" + Uri.EscapeDataString(psArgs);
    }

    public static string BuildWaitPath(string idOrName, string? condition)
    {
        condition = string.IsNullOrWhiteSpace(condition) ? "not-running" : condition;
        if (!WaitConditions.Contains(condition, StringComparer.Ordinal))
        {
            throw InvalidArgument("Argument 'condition' must be one of: not-running, next-exit, removed.");
        }

        return $"/containers/{Uri.EscapeDataString(idOrName)}/wait?condition={Uri.EscapeDataString(condition)}";
    }

    public static TimeSpan? NormalizeWaitTimeout(int? timeoutSeconds)
    {
        if (timeoutSeconds is null)
        {
            return null;
        }

        if (timeoutSeconds.Value < 0)
        {
            throw InvalidArgument("Argument 'timeoutSeconds' must be greater than or equal to 0.");
        }

        return TimeSpan.FromSeconds(timeoutSeconds.Value);
    }

    public static string BuildStopPath(string idOrName, int? timeoutSeconds)
    {
        var path = $"/containers/{Uri.EscapeDataString(idOrName)}/stop";
        return timeoutSeconds is null ? path : path + $"?t={ValidateTimeoutSeconds(timeoutSeconds.Value)}";
    }

    public static string BuildRestartPath(string idOrName, int? timeoutSeconds)
    {
        var path = $"/containers/{Uri.EscapeDataString(idOrName)}/restart";
        return timeoutSeconds is null ? path : path + $"?t={ValidateTimeoutSeconds(timeoutSeconds.Value)}";
    }

    public static string BuildKillPath(string idOrName, string? signal)
    {
        var path = $"/containers/{Uri.EscapeDataString(idOrName)}/kill";
        return string.IsNullOrWhiteSpace(signal) ? path : path + "?signal=" + Uri.EscapeDataString(signal);
    }

    public static string BuildLogsPath(string idOrName, bool follow, JsonElement args)
    {
        var query = new List<string>
        {
            "stdout=true",
            "stderr=true",
            "follow=" + follow.ToString().ToLowerInvariant()
        };

        if (ToolArgumentReader.OptionalString(args, "tail") is { } tail)
        {
            query.Add("tail=" + Uri.EscapeDataString(tail));
        }

        if (ToolArgumentReader.OptionalString(args, "since") is { } since)
        {
            query.Add("since=" + Uri.EscapeDataString(since));
        }

        if (ToolArgumentReader.OptionalBool(args, "timestamps"))
        {
            query.Add("timestamps=true");
        }

        return $"/containers/{Uri.EscapeDataString(idOrName)}/logs?{string.Join('&', query)}";
    }

    public static string BuildPrunePath(JsonElement args)
    {
        var filters = new JsonObject();

        if (ToolArgumentReader.OptionalString(args, "until") is { } until)
        {
            filters["until"] = StringArray([until]);
        }

        if (ToolArgumentReader.OptionalStringArray(args, "labels") is { Length: > 0 } labels)
        {
            filters["label"] = StringArray(labels);
        }

        if (ToolArgumentReader.OptionalStringArray(args, "labelNe") is { Length: > 0 } labelNe)
        {
            filters["label!"] = StringArray(labelNe);
        }

        return filters.Count == 0
            ? "/containers/prune"
            : "/containers/prune?filters=" + Uri.EscapeDataString(filters.ToCompactJson());
    }

    private static int ValidateTimeoutSeconds(int timeoutSeconds)
    {
        if (timeoutSeconds < 0)
        {
            throw new ContainerMcpException(
                McpErrorCode.InvalidArgument,
                "Argument 'timeoutSeconds' must be greater than or equal to 0.",
                StatusCodes.Status400BadRequest);
        }

        return timeoutSeconds;
    }

    private static JsonArray StringArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.AddNode(JsonValue.Create(value));
        }

        return array;
    }

    private static ContainerMcpException InvalidArgument(string message) =>
        new(McpErrorCode.InvalidArgument, message, StatusCodes.Status400BadRequest);
}
