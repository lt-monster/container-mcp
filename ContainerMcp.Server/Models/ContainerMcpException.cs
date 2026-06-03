using System.Text.Json.Nodes;

namespace ContainerMcp.Models;

internal sealed class ContainerMcpException : Exception
{
    public ContainerMcpException(
        string errorCode,
        string message,
        int statusCode = StatusCodes.Status500InternalServerError,
        string? endpoint = null,
        JsonObject? details = null)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
        Endpoint = endpoint;
        Details = details;
    }

    public string ErrorCode { get; }
    public int StatusCode { get; }
    public string? Endpoint { get; }
    public JsonObject? Details { get; }
}
