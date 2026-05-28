using ContainerMcp.Models;
using System.Text;
using System.Text.Json.Nodes;

namespace ContainerMcp.Mcp;

internal sealed class StdioMcpServer
{
    private readonly McpJsonRpcHandler _handler;

    public StdioMcpServer(McpJsonRpcHandler handler) => _handler = handler;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var input = Console.OpenStandardInput();
        var output = Console.OpenStandardOutput();
        using var reader = new StreamReader(input, Console.InputEncoding, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        await using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true)
        {
            AutoFlush = true,
            NewLine = "\n"
        };

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JsonObject response;
            try
            {
                using var document = JsonDocument.Parse(line);
                response = await _handler.HandleAsync(document.RootElement, cancellationToken);
            }
            catch (JsonException ex)
            {
                response = McpJsonRpcHandler.Error(null, -32700, ex.Message);
            }

            await writer.WriteLineAsync(response.ToCompactJson());
        }
    }
}
