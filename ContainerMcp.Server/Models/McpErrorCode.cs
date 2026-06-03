namespace ContainerMcp.Models;

internal static class McpErrorCode
{
    public const string EngineNotFound = "ENGINE_NOT_FOUND";
    public const string EngineUnavailable = "ENGINE_UNAVAILABLE";
    public const string ApiUnavailable = "API_UNAVAILABLE";
    public const string InvalidArgument = "INVALID_ARGUMENT";
    public const string UnsupportedTarget = "UNSUPPORTED_TARGET";
    public const string VolumeNotFound = "VOLUME_NOT_FOUND";
    public const string NetworkNotFound = "NETWORK_NOT_FOUND";
    public const string UnsupportedVolumeMount = "UNSUPPORTED_VOLUME_MOUNT";
    public const string ContainerNotFound = "CONTAINER_NOT_FOUND";
    public const string ImageNotFound = "IMAGE_NOT_FOUND";
    public const string PortRangeExhausted = "PORT_RANGE_EXHAUSTED";
    public const string OperationFailed = "OPERATION_FAILED";
}
