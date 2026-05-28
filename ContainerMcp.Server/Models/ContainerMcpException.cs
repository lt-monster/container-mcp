namespace ContainerMcp.Models;

internal sealed class ContainerMcpException : Exception
{
    public ContainerMcpException(string errorCode, string message, int statusCode = StatusCodes.Status500InternalServerError, string? endpoint = null)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
        Endpoint = endpoint;
    }

    public string ErrorCode { get; }
    public int StatusCode { get; }
    public string? Endpoint { get; }
}
