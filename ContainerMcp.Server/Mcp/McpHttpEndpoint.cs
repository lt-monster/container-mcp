using ContainerMcp.Models;
using ContainerMcp.Configuration;
using System.Text.Json.Nodes;

namespace ContainerMcp.Mcp;

internal static class McpHttpEndpoint
{
    public static async Task<IResult> HandlePostAsync(HttpContext httpContext, McpJsonRpcHandler handler, long maxRequestBodyBytes = ProgramSupport.MaxMcpHttpRequestBodyBytes)
        => await HandlePostAsync(httpContext, handler, options: null, maxRequestBodyBytes);

    public static async Task<IResult> HandlePostAsync(HttpContext httpContext, McpJsonRpcHandler handler, ContainerMcpOptions? options)
        => await HandlePostAsync(httpContext, handler, options, options?.MaxHttpRequestBodyBytes ?? ProgramSupport.MaxMcpHttpRequestBodyBytes);

    public static async Task<IResult> HandlePostAsync(HttpContext httpContext, McpJsonRpcHandler handler, ContainerMcpOptions? options, long maxRequestBodyBytes)
    {
        try
        {
            var logContext = LogContext(httpContext, tokenId: null);
            if (options is not null)
            {
                var authorization = HttpTokenValidator.TryAuthorize(options, httpContext.Request.Headers.Authorization.ToString());
                if (!authorization.IsAuthorized)
                {
                    Console.Error.WriteLine($"warn: mcp auth failed remote={Remote(httpContext)} reason={authorization.Reason}");
                    return Results.Unauthorized();
                }

                if (options.HttpTokens.Count > 0)
                {
                    Console.Error.WriteLine($"info: mcp auth accepted remote={Remote(httpContext)} tokenId={authorization.TokenId}");
                }

                logContext = LogContext(httpContext, authorization.TokenId);
            }

            if (httpContext.Request.ContentLength is > 0 && httpContext.Request.ContentLength > maxRequestBodyBytes)
            {
                return JsonNodeExtensions.JsonResult(
                    McpJsonRpcHandler.Error(null, -32600, $"MCP HTTP request body exceeds maxBytes ({maxRequestBodyBytes})."),
                    StatusCodes.Status413PayloadTooLarge);
            }

            await using var body = await ReadBodyAsync(httpContext.Request.Body, maxRequestBodyBytes, httpContext.RequestAborted);
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: httpContext.RequestAborted);
            var response = await handler.HandleMessageAsync(document.RootElement, httpContext.RequestAborted, logContext);
            if (response is null)
            {
                return Results.Accepted();
            }

            return JsonNodeExtensions.JsonResult(response);
        }
        catch (JsonException ex)
        {
            return JsonNodeExtensions.JsonResult(McpJsonRpcHandler.Error(null, -32700, ex.Message), StatusCodes.Status400BadRequest);
        }
        catch (McpRequestBodyTooLargeException ex)
        {
            return JsonNodeExtensions.JsonResult(
                McpJsonRpcHandler.Error(null, -32600, $"MCP HTTP request body exceeds maxBytes ({ex.MaxBytes})."),
                StatusCodes.Status413PayloadTooLarge);
        }
    }

    public static IResult HandleGet() =>
        JsonNodeExtensions.JsonResult(
            McpJsonRpcHandler.Error(null, -32600, "Streamable HTTP GET is not available because server-sent event streaming is not implemented."),
            StatusCodes.Status405MethodNotAllowed);

    public static IResult HandleHealth() =>
        JsonNodeExtensions.JsonResult(new JsonObject
        {
            ["status"] = "ok",
            ["service"] = "container-mcp"
        });

    public static IResult HandleReady(ContainerMcpOptions options) =>
        JsonNodeExtensions.JsonResult(new JsonObject
        {
            ["status"] = "ready",
            ["service"] = "container-mcp",
            ["version"] = ServerVersion.Current,
            ["transport"] = "http",
            ["endpoint"] = "/mcp",
            ["defaultEngine"] = options.DefaultEngine.ToString().ToLowerInvariant(),
            ["defaultTarget"] = options.DefaultTarget,
            ["httpAuthRequired"] = options.HttpTokens.Count > 0
        });

    public static IResult HandleUnsupportedMethod() =>
        JsonNodeExtensions.JsonResult(McpJsonRpcHandler.Error(null, -32600, "Unsupported MCP HTTP method."), StatusCodes.Status405MethodNotAllowed);

    private static async Task<Stream> ReadBodyAsync(Stream input, long maxRequestBodyBytes, CancellationToken cancellationToken)
    {
        var output = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                output.Position = 0;
                return output;
            }

            if (output.Length + read > maxRequestBodyBytes)
            {
                throw new McpRequestBodyTooLargeException(maxRequestBodyBytes);
            }

            output.Write(buffer, 0, read);
        }
    }

    private sealed class McpRequestBodyTooLargeException(long maxBytes) : Exception
    {
        public long MaxBytes { get; } = maxBytes;
    }

    private static McpRequestLogContext LogContext(HttpContext httpContext, string? tokenId) =>
        new(Remote(httpContext), tokenId);

    private static string Remote(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
