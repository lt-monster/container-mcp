using System.Text;
using System.Text.Json.Nodes;

namespace ContainerMcp.ContainerRuntime;

internal static class DockerRawStreamDecoder
{
    public static JsonObject Decode(byte[] bytes, int maxBytes)
    {
        maxBytes = Math.Max(0, maxBytes);
        return LooksLikeRawStream(bytes)
            ? DecodeFramed(bytes, maxBytes)
            : DecodePlain(bytes, maxBytes);
    }

    private static JsonObject DecodeFramed(byte[] bytes, int maxBytes)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var text = new StringBuilder();
        var offset = 0;
        var bytesRead = 0;
        var frameCount = 0;
        var truncated = false;

        while (offset + 8 <= bytes.Length && bytesRead < maxBytes)
        {
            var streamType = bytes[offset];
            var length = ReadBigEndianInt32(bytes, offset + 4);
            offset += 8;
            if (length < 0)
            {
                truncated = true;
                break;
            }

            var remaining = maxBytes - bytesRead;
            var available = Math.Min(length, bytes.Length - offset);
            var take = Math.Min(available, remaining);
            var payload = Encoding.UTF8.GetString(bytes, offset, take);
            Append(streamType, payload, stdout, stderr);
            text.Append(payload);

            bytesRead += take;
            frameCount++;
            offset += available;
            if (available < length || take < length)
            {
                truncated = true;
                break;
            }
        }

        if (offset < bytes.Length && bytesRead >= maxBytes)
        {
            truncated = true;
        }

        return Result(stdout.ToString(), stderr.ToString(), text.ToString(), bytesRead, frameCount, truncated, framed: true);
    }

    private static JsonObject DecodePlain(byte[] bytes, int maxBytes)
    {
        var take = Math.Min(bytes.Length, maxBytes);
        var text = Encoding.UTF8.GetString(bytes, 0, take);
        return Result(text, string.Empty, text, take, 0, take < bytes.Length, framed: false);
    }

    private static bool LooksLikeRawStream(byte[] bytes)
    {
        if (bytes.Length < 8)
        {
            return false;
        }

        var streamType = bytes[0];
        if (streamType is < 0 or > 2 || bytes[1] != 0 || bytes[2] != 0 || bytes[3] != 0)
        {
            return false;
        }

        var length = ReadBigEndianInt32(bytes, 4);
        return length >= 0;
    }

    private static void Append(byte streamType, string payload, StringBuilder stdout, StringBuilder stderr)
    {
        if (streamType == 2)
        {
            stderr.Append(payload);
        }
        else
        {
            stdout.Append(payload);
        }
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset) =>
        (bytes[offset] << 24)
        | (bytes[offset + 1] << 16)
        | (bytes[offset + 2] << 8)
        | bytes[offset + 3];

    private static JsonObject Result(
        string stdout,
        string stderr,
        string text,
        int bytesRead,
        int frameCount,
        bool truncated,
        bool framed) => new()
        {
            ["stdout"] = stdout,
            ["stderr"] = stderr,
            ["text"] = text,
            ["bytesRead"] = bytesRead,
            ["frameCount"] = frameCount,
            ["truncated"] = truncated,
            ["framed"] = framed
        };
}
