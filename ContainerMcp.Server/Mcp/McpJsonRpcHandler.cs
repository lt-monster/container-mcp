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

    public async Task<JsonNode?> HandleMessageAsync(JsonElement message, CancellationToken cancellationToken)
        => await HandleMessageAsync(message, cancellationToken, McpRequestLogContext.Stdio);

    public async Task<JsonNode?> HandleMessageAsync(JsonElement message, CancellationToken cancellationToken, McpRequestLogContext logContext)
    {
        return message.ValueKind switch
        {
            JsonValueKind.Object => await HandleAsync(message, cancellationToken, logContext),
            JsonValueKind.Array => await HandleBatchAsync(message, cancellationToken, logContext),
            _ => Error(null, -32600, "Invalid JSON-RPC request.")
        };
    }

    public async Task<JsonObject?> HandleAsync(JsonElement request, CancellationToken cancellationToken)
        => await HandleAsync(request, cancellationToken, McpRequestLogContext.Stdio);

    public async Task<JsonObject?> HandleAsync(JsonElement request, CancellationToken cancellationToken, McpRequestLogContext logContext)
    {
        if (request.ValueKind != JsonValueKind.Object)
        {
            return Error(null, -32600, "Invalid JSON-RPC request.");
        }

        var hasId = request.TryGetProperty("id", out var idProperty);
        var hasValidId = !hasId || IsValidId(idProperty);
        var id = hasId && hasValidId ? idProperty.Clone() : default(JsonElement?);
        try
        {
            if (IsJsonRpcResponse(request))
            {
                return null;
            }

            if (!HasJsonRpcVersion(request) || !hasValidId)
            {
                return hasId ? Error(id, -32600, "Invalid JSON-RPC request.") : null;
            }

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
                "tools/call" => await HandleToolCallAsync(id, GetParams(request), cancellationToken, logContext),
                "notifications/initialized" => Result(id, new JsonObject()),
                _ => Error(id, -32601, $"Method '{method}' is not supported.")
            };

            return hasId ? response : null;
        }
        catch (ContainerMcpException ex)
        {
            return hasId ? Error(id, JsonRpcCode(ex), ex.Message, new JsonObject
            {
                ["errorCode"] = ex.ErrorCode,
                ["message"] = ex.Message,
                ["statusCode"] = ex.StatusCode,
                ["endpoint"] = ex.Endpoint,
                ["details"] = ex.Details.CloneNode()
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

    private async Task<JsonNode?> HandleBatchAsync(JsonElement batch, CancellationToken cancellationToken, McpRequestLogContext logContext)
    {
        if (batch.GetArrayLength() == 0)
        {
            return Error(null, -32600, "Invalid JSON-RPC batch request.");
        }

        var responses = new JsonArray();
        foreach (var item in batch.EnumerateArray())
        {
            JsonObject? response = item.ValueKind == JsonValueKind.Object
                ? await HandleAsync(item, cancellationToken, logContext)
                : Error(null, -32600, "Invalid JSON-RPC request.");

            if (response is not null)
            {
                responses.AddNode(response);
            }
        }

        return responses.Count == 0 ? null : responses;
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

    private async Task<JsonObject> HandleToolCallAsync(JsonElement? id, JsonElement parameters, CancellationToken cancellationToken, McpRequestLogContext logContext)
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
        var requestId = LogId(id);
        var startTimestamp = McpToolLogger.Timestamp();
        McpToolLogger.Start(requestId, name, arguments, logContext);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.ToolTimeout);

        try
        {
            var call = _tools.CallAsync(name, arguments, timeout.Token);
            result = await call.WaitAsync(_options.ToolTimeout, cancellationToken);
            McpToolLogger.Success(requestId, name, arguments, result, startTimestamp);
        }
        catch (TimeoutException)
        {
            timeout.Cancel();
            var exception = ToolTimeout(name);
            McpToolLogger.Error(requestId, name, arguments, exception, startTimestamp);
            throw exception;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var exception = ToolTimeout(name);
            McpToolLogger.Error(requestId, name, arguments, exception, startTimestamp);
            throw exception;
        }
        catch (ContainerMcpException ex)
        {
            McpToolLogger.Error(requestId, name, arguments, ex, startTimestamp);
            throw;
        }
        catch (Exception ex)
        {
            McpToolLogger.Error(requestId, name, arguments, ex, startTimestamp);
            throw;
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

    private ContainerMcpException ToolTimeout(string name) =>
        new(
            McpErrorCode.EngineUnavailable,
            $"Tool '{name}' timed out after {_options.ToolTimeout.TotalSeconds:0} seconds.",
            StatusCodes.Status504GatewayTimeout);

    private static int JsonRpcCode(ContainerMcpException ex) => ex.ErrorCode switch
    {
        McpErrorCode.InvalidArgument => -32602,
        McpErrorCode.UnsupportedTarget => -32602,
        McpErrorCode.UnsupportedVolumeMount => -32602,
        McpErrorCode.EngineNotFound => -32001,
        McpErrorCode.EngineUnavailable => -32002,
        McpErrorCode.ApiUnavailable => -32003,
        McpErrorCode.ContainerNotFound => -32004,
        McpErrorCode.ImageNotFound => -32005,
        McpErrorCode.VolumeNotFound => -32006,
        McpErrorCode.PortRangeExhausted => -32007,
        _ => -32000
    };

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
            ["version"] = ServerVersion.Current
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

    private static bool IsJsonRpcResponse(JsonElement request) =>
        !request.TryGetProperty("method", out _) &&
        request.TryGetProperty("id", out _) &&
        (request.TryGetProperty("result", out _) || request.TryGetProperty("error", out _));

    private static bool HasJsonRpcVersion(JsonElement request) =>
        request.TryGetProperty("jsonrpc", out var jsonrpc)
        && jsonrpc.ValueKind == JsonValueKind.String
        && string.Equals(jsonrpc.GetString(), "2.0", StringComparison.Ordinal);

    private static bool IsValidId(JsonElement id) =>
        id.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.Null;

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

    private static string LogId(JsonElement? id)
    {
        if (id is null || id.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return "-";
        }

        return id.Value.ValueKind switch
        {
            JsonValueKind.Number when id.Value.TryGetInt64(out var number) => number.ToString(CultureInfo.InvariantCulture),
            JsonValueKind.String => id.Value.GetString() ?? "-",
            _ => "-"
        };
    }
}
