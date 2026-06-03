using ContainerMcp.Configuration;

namespace ContainerMcp.Server.Tests;

public sealed class HttpTokenConfigurationTests
{
    [Fact]
    public void ValidateForStartup_AllowsLoopbackWithoutTokens()
    {
        var options = ContainerMcpOptions.From(["--urls", "http://127.0.0.1:7010"]);

        HttpTokenValidator.ValidateForStartup(options);
    }

    [Fact]
    public void ValidateForStartup_RejectsNonLoopbackWithoutTokens()
    {
        var options = ContainerMcpOptions.From(["--urls", "http://0.0.0.0:7010"]);

        var exception = Assert.Throws<InvalidOperationException>(() => HttpTokenValidator.ValidateForStartup(options));

        Assert.Contains("requires at least one HTTP bearer token", exception.Message);
    }

    [Fact]
    public void IsAuthorized_RequiresBearerTokenWhenTokensAreConfigured()
    {
        var token = "cmcp_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMN";
        var options = new ContainerMcpOptions(
            TransportMode.Http,
            "http://127.0.0.1:7010",
            ContainerEngine.Auto,
            "local",
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(15),
            ProgramSupport.MaxMcpHttpRequestBodyBytes,
            [new HttpToken("default", token, true, null, null)]);

        Assert.False(HttpTokenValidator.IsAuthorized(options, null));
        Assert.False(HttpTokenValidator.IsAuthorized(options, "Bearer cmcp_wrongabcdefghijklmnopqrstuvwxyzABCDEF"));
        Assert.True(HttpTokenValidator.IsAuthorized(options, "Bearer " + token));
    }

    [Fact]
    public void IsAuthorized_IgnoresDisabledTokens()
    {
        var token = "cmcp_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMN";
        var options = new ContainerMcpOptions(
            TransportMode.Http,
            "http://127.0.0.1:7010",
            ContainerEngine.Auto,
            "local",
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(15),
            ProgramSupport.MaxMcpHttpRequestBodyBytes,
            [new HttpToken("default", token, false, null, null)]);

        Assert.False(HttpTokenValidator.IsAuthorized(options, "Bearer " + token));
    }
}
