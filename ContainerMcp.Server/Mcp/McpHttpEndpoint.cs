using ContainerMcp.Models;

namespace ContainerMcp.Mcp;

internal static class McpHttpEndpoint
{
    public static async Task<IResult> HandlePostAsync(HttpContext httpContext, McpJsonRpcHandler handler)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(httpContext.Request.Body, cancellationToken: httpContext.RequestAborted);
            var response = await handler.HandleMessageAsync(document.RootElement, httpContext.RequestAborted);
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
    }

    public static IResult HandleGet() =>
        JsonNodeExtensions.JsonResult(
            McpJsonRpcHandler.Error(null, -32600, "Streamable HTTP GET is not available because server-sent event streaming is not implemented."),
            StatusCodes.Status405MethodNotAllowed);

    public static IResult HandleUnsupportedMethod() =>
        JsonNodeExtensions.JsonResult(McpJsonRpcHandler.Error(null, -32600, "Unsupported MCP HTTP method."), StatusCodes.Status405MethodNotAllowed);
}
