using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using ContainerMcp.Configuration;
using ContainerMcp.Models;

namespace ContainerMcp.ContainerRuntime;

internal sealed class ContainerApiAdapter
{
    private readonly DockerApiClientFactory _factory;
    private readonly ContainerMcpOptions _options;

    public ContainerApiAdapter(DockerApiClientFactory factory, ContainerMcpOptions options)
    {
        _factory = factory;
        _options = options;
    }

    public Task<JsonElement> GetAsync(ResolvedEngine engine, string path, CancellationToken cancellationToken) =>
        SendAsync(engine, HttpMethod.Get, path, null, cancellationToken);

    public Task<JsonElement> PostAsync(ResolvedEngine engine, string path, JsonNode? body, CancellationToken cancellationToken) =>
        SendAsync(engine, HttpMethod.Post, path, body, cancellationToken);

    public Task<JsonElement> DeleteAsync(ResolvedEngine engine, string path, CancellationToken cancellationToken) =>
        SendAsync(engine, HttpMethod.Delete, path, null, cancellationToken);

    public async Task<string> GetStringAsync(ResolvedEngine engine, string path, CancellationToken cancellationToken)
    {
        try
        {
            return await GetStringCoreAsync(engine, path, cancellationToken).WaitAsync(_options.ApiTimeout, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw Timeout(engine);
        }
        catch (TimeoutException)
        {
            throw Timeout(engine);
        }
        catch (IOException ex)
        {
            throw Unavailable(engine, ex);
        }
        catch (SocketException ex)
        {
            throw Unavailable(engine, ex);
        }
        catch (HttpRequestException ex)
        {
            throw Unavailable(engine, ex);
        }
    }

    private async Task<JsonElement> SendAsync(ResolvedEngine engine, HttpMethod method, string path, JsonNode? body, CancellationToken cancellationToken)
    {
        try
        {
            return await SendCoreAsync(engine, method, path, body, cancellationToken).WaitAsync(_options.ApiTimeout, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw Timeout(engine);
        }
        catch (TimeoutException)
        {
            throw Timeout(engine);
        }
        catch (IOException ex)
        {
            throw Unavailable(engine, ex);
        }
        catch (SocketException ex)
        {
            throw Unavailable(engine, ex);
        }
        catch (HttpRequestException ex)
        {
            throw Unavailable(engine, ex);
        }
    }

    private async Task<string> GetStringCoreAsync(ResolvedEngine engine, string path, CancellationToken cancellationToken)
    {
        using var client = _factory.Create(engine.Endpoint);
        using var response = await client.GetAsync(path, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            ThrowRuntimeError(engine, path, response.StatusCode, body);
        }

        return body;
    }

    private async Task<JsonElement> SendCoreAsync(ResolvedEngine engine, HttpMethod method, string path, JsonNode? body, CancellationToken cancellationToken)
    {
        using var client = _factory.Create(engine.Endpoint);
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = new StringContent(body.ToCompactJson(), Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            ThrowRuntimeError(engine, path, response.StatusCode, text);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return JsonDocument.Parse("""{"ok":true}""").RootElement.Clone();
        }

        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private static void ThrowRuntimeError(ResolvedEngine engine, string path, HttpStatusCode statusCode, string body)
    {
        var message = ExtractMessage(body);
        var code = statusCode switch
        {
            HttpStatusCode.NotFound when path.Contains("/containers/", StringComparison.OrdinalIgnoreCase) => McpErrorCode.ContainerNotFound,
            HttpStatusCode.NotFound when path.Contains("/images/", StringComparison.OrdinalIgnoreCase) => McpErrorCode.ImageNotFound,
            HttpStatusCode.NotFound when path.Contains("/volumes/", StringComparison.OrdinalIgnoreCase) => McpErrorCode.VolumeNotFound,
            HttpStatusCode.BadRequest => McpErrorCode.InvalidArgument,
            HttpStatusCode.InternalServerError => McpErrorCode.OperationFailed,
            _ => McpErrorCode.EngineUnavailable
        };

        throw new ContainerMcpException(
            code,
            string.IsNullOrWhiteSpace(message) ? $"{engine.Engine} API returned {(int)statusCode}." : message,
            (int)statusCode,
            engine.Endpoint.ToString());
    }

    private static string? ExtractMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("message", out var message))
            {
                return message.GetString();
            }
        }
        catch (JsonException)
        {
            return body;
        }

        return body;
    }

    private static ContainerMcpException Timeout(ResolvedEngine engine) =>
        new(
            McpErrorCode.EngineUnavailable,
            $"{engine.Engine} API request timed out at {engine.Endpoint}.",
            StatusCodes.Status504GatewayTimeout,
            engine.Endpoint.ToString());

    private static ContainerMcpException Unavailable(ResolvedEngine engine, Exception ex) =>
        new(
            McpErrorCode.EngineUnavailable,
            $"{engine.Engine} API is unavailable at {engine.Endpoint}: {ex.Message}",
            StatusCodes.Status503ServiceUnavailable,
            engine.Endpoint.ToString());
}
