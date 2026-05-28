using ContainerMcp.Models;

namespace ContainerMcp.ContainerRuntime;

internal sealed class VolumePolicy
{
    public string[] ValidateContainerCreateVolumes(string[]? volumes)
    {
        if (volumes is null || volumes.Length == 0)
        {
            return [];
        }

        foreach (var volume in volumes)
        {
            if (string.IsNullOrWhiteSpace(volume) || !volume.Contains(':', StringComparison.Ordinal))
            {
                throw new ContainerMcpException(McpErrorCode.InvalidArgument, "Volume mounts must use source:target[:mode].", StatusCodes.Status400BadRequest);
            }

            var source = GetMountSource(volume);
            if (LooksLikeHostPath(source))
            {
                throw new ContainerMcpException(
                    McpErrorCode.UnsupportedVolumeMount,
                    "v1 supports named or anonymous container volumes only; host directory bind mounts are not supported.",
                    StatusCodes.Status400BadRequest);
            }
        }

        return volumes;
    }

    private static string GetMountSource(string volume)
    {
        if (volume.Length >= 3 && char.IsAsciiLetter(volume[0]) && volume[1] == ':' && (volume[2] == '\\' || volume[2] == '/'))
        {
            var separator = volume.IndexOf(':', 2);
            return separator < 0 ? volume : volume[..separator];
        }

        var firstSeparator = volume.IndexOf(':', StringComparison.Ordinal);
        return firstSeparator < 0 ? volume : volume[..firstSeparator];
    }

    private static bool LooksLikeHostPath(string source)
    {
        if (source is "" or ".")
        {
            return true;
        }

        if (source.StartsWith("/", StringComparison.Ordinal) || source.StartsWith("~/", StringComparison.Ordinal))
        {
            return true;
        }

        if (source.StartsWith("./", StringComparison.Ordinal) || source.StartsWith("../", StringComparison.Ordinal))
        {
            return true;
        }

        if (source.Length >= 3 && char.IsAsciiLetter(source[0]) && source[1] == ':' && (source[2] == '\\' || source[2] == '/'))
        {
            return true;
        }

        return source.StartsWith(@"\\", StringComparison.Ordinal);
    }
}
