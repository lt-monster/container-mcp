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

    private static int ValidateTimeoutSeconds(int timeoutSeconds)
    {
        if (timeoutSeconds < 0)
        {
            throw new ContainerMcp.Models.ContainerMcpException(
                ContainerMcp.Models.McpErrorCode.InvalidArgument,
                "Argument 'timeoutSeconds' must be greater than or equal to 0.",
                StatusCodes.Status400BadRequest);
        }

        return timeoutSeconds;
    }

    private static ContainerMcp.Models.ContainerMcpException InvalidArgument(string message) =>
        new(ContainerMcp.Models.McpErrorCode.InvalidArgument, message, StatusCodes.Status400BadRequest);
}
