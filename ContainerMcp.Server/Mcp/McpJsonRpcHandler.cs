using ContainerMcp.Configuration;
using ContainerMcp.Models;
using System.Text.Json.Nodes;

namespace ContainerMcp.Mcp;

internal sealed class McpJsonRpcHandler
{
    private readonly IMcpToolRegistry _tools;
    private readonly ContainerMcpOptions _options;

    public McpJsonRpcHandler(IMcpToolRegistry tools, ContainerMcpOptions options)
    {
        _tools = tools;
        _options = options;
    }

    public async Task<JsonObject?> HandleAsync(JsonElement request, CancellationToken cancellationToken)
    {
        var hasId = request.TryGetProperty("id", out var idProperty);
        var id = hasId ? idProperty.Clone() : default(JsonElement?);
        try
        {
            if (!request.TryGetProperty("method", out var methodProperty) || methodProperty.ValueKind != JsonValueKind.String)
            {
                return hasId ? Error(id, -32600, "Invalid JSON-RPC request.") : null;
            }

            var method = methodProperty.GetString();
            var response = method switch
            {
                "initialize" => Result(id, InitializeResult()),
                "ping" => Result(id, new JsonObject()),
                "tools/list" => Result(id, new JsonObject { ["tools"] = _tools.List() }),
                "tools/call" => await HandleToolCallAsync(id, GetParams(request), cancellationToken),
                "notifications/initialized" => Result(id, new JsonObject()),
                _ => Error(id, -32601, $"Method '{method}' is not supported.")
            };

            return hasId ? response : null;
        }
        catch (ContainerMcpException ex)
        {
            return hasId ? Error(id, -32000, ex.Message, new JsonObject
            {
                ["errorCode"] = ex.ErrorCode,
                ["message"] = ex.Message,
                ["statusCode"] = ex.StatusCode,
                ["endpoint"] = ex.Endpoint
            }) : null;
        }
        catch (Exception ex)
        {
            return hasId ? Error(id, -32603, ex.Message, new JsonObject
            {
                ["errorCode"] = McpErrorCode.OperationFailed,
                ["message"] = ex.Message
            }) : null;
        }
    }

    public static JsonObject Error(JsonElement? id, int code, string message, JsonNode? data = null) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = JsonId(id),
        ["error"] = new JsonObject
        {
            ["code"] = code,
            ["message"] = message,
            ["data"] = data
        }
    };

    private async Task<JsonObject> HandleToolCallAsync(JsonElement? id, JsonElement parameters, CancellationToken cancellationToken)
    {
        if (!parameters.TryGetProperty("name", out var nameProperty) || nameProperty.ValueKind != JsonValueKind.String)
        {
            return Error(id, -32602, "tools/call requires a tool name.");
        }

        var name = nameProperty.GetString()!;
        var arguments = parameters.TryGetProperty("arguments", out var argsProperty) && argsProperty.ValueKind == JsonValueKind.Object
            ? argsProperty
            : EmptyArguments();

        JsonObject result;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.ToolTimeout);
        var call = _tools.CallAsync(name, arguments, timeout.Token);

        try
        {
            result = await call.WaitAsync(_options.ToolTimeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            timeout.Cancel();
            await ObserveTimedOutCallAsync(call);
            throw ToolTimeout(name);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw ToolTimeout(name);
        }

        return Result(id, new JsonObject
        {
            ["content"] = JsonNodeExtensions.Array(new JsonObject
            {
                ["type"] = "text",
                ["text"] = result.ToCompactJson()
            }),
            ["structuredContent"] = result.DeepClone()
        });
    }

    private static JsonElement EmptyArguments()
    {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }

    private static async Task ObserveTimedOutCallAsync(Task<JsonObject> call)
    {
        try
        {
            await call.WaitAsync(TimeSpan.FromMilliseconds(100));
        }
        catch
        {
            // The MCP response should be a timeout regardless of how the tool finishes after cancellation.
        }
    }

    private ContainerMcpException ToolTimeout(string name) =>
        new(
            McpErrorCode.EngineUnavailable,
            $"Tool '{name}' timed out after {_options.ToolTimeout.TotalSeconds:0} seconds.",
            StatusCodes.Status504GatewayTimeout);

    private static JsonObject Result(JsonElement? id, JsonNode? result) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = JsonId(id),
        ["result"] = result
    };

    private static JsonObject InitializeResult() => new()
    {
        ["protocolVersion"] = "2024-11-05",
        ["serverInfo"] = new JsonObject
        {
            ["name"] = "container-mcp",
            ["version"] = "0.1.0"
        },
        ["capabilities"] = new JsonObject
        {
            ["tools"] = new JsonObject()
        }
    };

    private static JsonElement GetParams(JsonElement request)
    {
        if (request.TryGetProperty("params", out var parameters) && parameters.ValueKind == JsonValueKind.Object)
        {
            return parameters;
        }

        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }

    private static JsonNode? JsonId(JsonElement? id)
    {
        if (id is null || id.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        return id.Value.ValueKind switch
        {
            JsonValueKind.Number when id.Value.TryGetInt64(out var number) => JsonValue.Create(number),
            JsonValueKind.String => JsonValue.Create(id.Value.GetString()),
            _ => id.Value.ToJsonNode()
        };
    }
}
