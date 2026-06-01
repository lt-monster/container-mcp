namespace ContainerMcp.Tools;

internal static class ContainerToolRequests
{
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
}
