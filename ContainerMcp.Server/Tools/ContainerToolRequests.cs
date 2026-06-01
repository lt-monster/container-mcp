namespace ContainerMcp.Tools;

internal static class ContainerToolRequests
{
    public static string BuildRestartPath(string idOrName, int? timeoutSeconds)
    {
        var path = $"/containers/{Uri.EscapeDataString(idOrName)}/restart";
        return timeoutSeconds is null ? path : path + $"?t={timeoutSeconds.Value}";
    }
}
