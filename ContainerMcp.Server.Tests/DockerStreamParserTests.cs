using System.Text;
using ContainerMcp.ContainerRuntime;

namespace ContainerMcp.Server.Tests;

public sealed class DockerStreamParserTests
{
    [Fact]
    public void ParseJsonLines_ReturnsEventsAndLastStatus()
    {
        const string text = """
{"status":"Pulling from library/nginx","id":"latest"}
{"status":"Download complete","id":"layer-1"}
{"status":"Status: Downloaded newer image for nginx:latest"}
""";

        var result = DockerJsonMessageStream.Parse(text, maxEvents: 10);

        Assert.Equal(3, result["eventCount"]!.GetValue<int>());
        Assert.False(result["truncated"]!.GetValue<bool>());
        Assert.Equal("Status: Downloaded newer image for nginx:latest", result["lastStatus"]!.GetValue<string>());
        Assert.Equal("Download complete", result["events"]![1]!["status"]!.GetValue<string>());
    }

    [Fact]
    public void DecodeRawStream_SeparatesStdoutAndStderr()
    {
        var bytes = RawFrame(1, "hello\n").Concat(RawFrame(2, "error\n")).ToArray();

        var result = DockerRawStreamDecoder.Decode(bytes, maxBytes: 1024);

        Assert.Equal("hello\n", result["stdout"]!.GetValue<string>());
        Assert.Equal("error\n", result["stderr"]!.GetValue<string>());
        Assert.Equal("hello\nerror\n", result["text"]!.GetValue<string>());
        Assert.Equal(2, result["frameCount"]!.GetValue<int>());
        Assert.False(result["truncated"]!.GetValue<bool>());
    }

    [Fact]
    public void DecodeRawStream_TruncatesAtMaxBytes()
    {
        var bytes = RawFrame(1, "abcdef");

        var result = DockerRawStreamDecoder.Decode(bytes, maxBytes: 3);

        Assert.Equal("abc", result["stdout"]!.GetValue<string>());
        Assert.Equal(3, result["bytesRead"]!.GetValue<int>());
        Assert.True(result["truncated"]!.GetValue<bool>());
    }

    [Fact]
    public void DecodeRawStream_ReturnsPartialPayloadWhenFrameIsIncomplete()
    {
        var bytes = RawFrame(1, "abcdef").Take(11).ToArray();

        var result = DockerRawStreamDecoder.Decode(bytes, maxBytes: 1024);

        Assert.Equal("abc", result["stdout"]!.GetValue<string>());
        Assert.Equal(3, result["bytesRead"]!.GetValue<int>());
        Assert.True(result["truncated"]!.GetValue<bool>());
    }

    private static byte[] RawFrame(byte streamType, string payload)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var frame = new byte[8 + payloadBytes.Length];
        frame[0] = streamType;
        frame[4] = (byte)(payloadBytes.Length >> 24);
        frame[5] = (byte)(payloadBytes.Length >> 16);
        frame[6] = (byte)(payloadBytes.Length >> 8);
        frame[7] = (byte)payloadBytes.Length;
        payloadBytes.CopyTo(frame, 8);
        return frame;
    }
}
