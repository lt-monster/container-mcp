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

    public Task<JsonObject> PostJsonMessageStreamAsync(ResolvedEngine engine, string path, JsonNode? body, int maxEvents, CancellationToken cancellationToken) =>
        SendJsonMessageStreamAsync(engine, HttpMethod.Post, path, body, null, null, maxEvents, cancellationToken);

    public Task<JsonObject> PostJsonMessageStreamAsync(ResolvedEngine engine, string path, JsonNode? body, IReadOnlyDictionary<string, string> headers, int maxEvents, CancellationToken cancellationToken) =>
        SendJsonMessageStreamAsync(engine, HttpMethod.Post, path, body, null, headers, maxEvents, cancellationToken);

    public Task<JsonObject> PostTarJsonMessageStreamAsync(ResolvedEngine engine, string path, Stream tarStream, int maxEvents, CancellationToken cancellationToken) =>
        SendJsonMessageStreamAsync(engine, HttpMethod.Post, path, null, tarStream, null, maxEvents, cancellationToken);

    public Task<JsonElement> DeleteAsync(ResolvedEngine engine, string path, CancellationToken cancellationToken) =>
        SendAsync(engine, HttpMethod.Delete, path, null, cancellationToken);

    public async Task<byte[]> GetBytesAsync(ResolvedEngine engine, string path, int maxBytes, CancellationToken cancellationToken)
    {
        try
        {
            return await GetBytesCoreAsync(engine, path, maxBytes, cancellationToken).WaitAsync(_options.ApiTimeout, cancellationToken);
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

    public async Task<JsonObject> GetToFileAsync(ResolvedEngine engine, string path, string outputPath, long maxBytes, CancellationToken cancellationToken)
    {
        try
        {
            return await GetToFileCoreAsync(engine, path, outputPath, maxBytes, cancellationToken).WaitAsync(_options.ApiTimeout, cancellationToken);
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

    private async Task<JsonObject> SendJsonMessageStreamAsync(ResolvedEngine engine, HttpMethod method, string path, JsonNode? body, Stream? tarStream, IReadOnlyDictionary<string, string>? headers, int maxEvents, CancellationToken cancellationToken)
    {
        try
        {
            return await SendJsonMessageStreamCoreAsync(engine, method, path, body, tarStream, headers, maxEvents, cancellationToken).WaitAsync(_options.ApiTimeout, cancellationToken);
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
        var client = _factory.GetClient(engine.Endpoint);
        using var response = await client.GetAsync(path, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            ThrowRuntimeError(engine, path, response.StatusCode, body);
        }

        return body;
    }

    private async Task<byte[]> GetBytesCoreAsync(ResolvedEngine engine, string path, int maxBytes, CancellationToken cancellationToken)
    {
        var client = _factory.GetClient(engine.Endpoint);
        using var response = await client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var body = await ReadAtMostAsync(stream, maxBytes, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            ThrowRuntimeError(engine, path, response.StatusCode, Encoding.UTF8.GetString(body));
        }

        return body;
    }

    private async Task<JsonObject> GetToFileCoreAsync(ResolvedEngine engine, string path, string outputPath, long maxBytes, CancellationToken cancellationToken)
    {
        maxBytes = Math.Max(1, maxBytes);
        var client = _factory.GetClient(engine.Endpoint);
        using var response = await client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            using var reader = new StreamReader(stream);
            ThrowRuntimeError(engine, path, response.StatusCode, await reader.ReadToEndAsync(cancellationToken));
        }

        var bytesWritten = 0L;
        var buffer = new byte[81920];
        try
        {
            await using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, useAsync: true);
            while (true)
            {
                var read = await stream.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                if (bytesWritten + read > maxBytes)
                {
                    throw new ContainerMcpException(
                        McpErrorCode.OperationFailed,
                        $"Docker API response exceeded maxBytes ({maxBytes}).",
                        StatusCodes.Status413PayloadTooLarge,
                        engine.Endpoint.ToString());
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                bytesWritten += read;
            }
        }
        catch
        {
            TryDelete(outputPath);
            throw;
        }

        return new JsonObject
        {
            ["outputPath"] = outputPath,
            ["bytesWritten"] = bytesWritten
        };
    }

    private async Task<JsonObject> SendJsonMessageStreamCoreAsync(ResolvedEngine engine, HttpMethod method, string path, JsonNode? body, Stream? tarStream, IReadOnlyDictionary<string, string>? headers, int maxEvents, CancellationToken cancellationToken)
    {
        var client = _factory.GetClient(engine.Endpoint);
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = new StringContent(body.ToCompactJson(), Encoding.UTF8, "application/json");
        }
        else if (tarStream is not null)
        {
            request.Content = new StreamContent(tarStream);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-tar");
        }

        if (headers is not null)
        {
            foreach (var header in headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            using var reader = new StreamReader(stream);
            ThrowRuntimeError(engine, path, response.StatusCode, await reader.ReadToEndAsync(cancellationToken));
        }

        return await DockerJsonMessageStream.ParseAsync(stream, maxEvents, cancellationToken);
    }

    private async Task<JsonElement> SendCoreAsync(ResolvedEngine engine, HttpMethod method, string path, JsonNode? body, CancellationToken cancellationToken)
    {
        var client = _factory.GetClient(engine.Endpoint);
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

    private static async Task<byte[]> ReadAtMostAsync(Stream stream, int maxBytes, CancellationToken cancellationToken)
    {
        maxBytes = Math.Max(1, maxBytes);
        using var output = new MemoryStream(capacity: Math.Min(maxBytes, 81920));
        var buffer = new byte[Math.Min(maxBytes, 81920)];
        while (output.Length < maxBytes)
        {
            var remaining = maxBytes - (int)output.Length;
            var read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), cancellationToken);
            if (read == 0)
            {
                break;
            }

            output.Write(buffer, 0, read);
        }

        return output.ToArray();
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
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
