using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace ProjectManagementApp.Tests;

public sealed class RuntimeIdentityTests
{
    private const string Key = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string Instance = "11111111111111111111111111111111";

    [Theory]
    [InlineData("api")]
    [InlineData("mcp")]
    public async Task RuntimeRoutesReturnSafeMetadataAndAuthenticatedGracefulShutdown(string role)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
            { Args = Array.Empty<string>(), ContentRootPath = AppContext.BaseDirectory });
        builder.Logging.ClearProviders();
        await using var app = builder.Build();
        var identity = new RuntimeIdentity(role, "http://localhost:5180/", Key, Instance);
        app.MapArcFlowRuntime(identity);
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(s => s.Endpoints)
            .OfType<RouteEndpoint>().ToArray();
        Assert.Equal(2, endpoints.Length);
        var port = role == "api" ? 5180 : 5190;
        var metadata = Context(app, port);
        await Invoke(endpoints, "/_arcflow/identity", metadata);
        Assert.Equal(200, metadata.Response.StatusCode);
        var json = JsonNode.Parse(((MemoryStream)metadata.Response.Body).ToArray())!;
        Assert.Equal("ArcFlow", json["app"]!.GetValue<string>());
        Assert.Equal(role, json["role"]!.GetValue<string>());
        Assert.Equal(1, json["protocol"]!.GetValue<int>());
        Assert.Equal(Environment.ProcessId, json["pid"]!.GetValue<int>());
        Assert.Equal(Instance, json["instanceId"]!.GetValue<string>());
        Assert.Equal(role == "mcp" ? "http://127.0.0.1:5180" : null, json["apiBaseUrl"]?.GetValue<string>());
        Assert.DoesNotContain(Key, json.ToJsonString());
        Assert.Equal("no-store", metadata.Response.Headers.CacheControl.ToString());

        var completed = new CompletionFeature();
        var shutdown = Context(app, port, completed);
        shutdown.Request.Headers[RuntimeIdentity.Header] = Key;
        shutdown.Request.Headers["X-ArcFlow-Instance"] = Instance;
        shutdown.Request.Headers["X-ArcFlow-Pid"] = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        await Invoke(endpoints, "/_arcflow/shutdown", shutdown);
        Assert.Equal(204, shutdown.Response.StatusCode);
        Assert.False(app.Lifetime.ApplicationStopping.IsCancellationRequested);
        await completed.CompleteAsync();
        Assert.True(app.Lifetime.ApplicationStopping.IsCancellationRequested);
    }

    [Theory]
    [InlineData("remote")]
    [InlineData("host")]
    [InlineData("origin")]
    [InlineData("key")]
    [InlineData("instance")]
    [InlineData("pid")]
    [InlineData("missing")]
    public async Task ShutdownRejectsWrongIdentityOrBrowserRequest(string wrong)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
            { Args = Array.Empty<string>(), ContentRootPath = AppContext.BaseDirectory });
        builder.Logging.ClearProviders();
        await using var app = builder.Build();
        app.MapArcFlowRuntime(new RuntimeIdentity("api", null, Key, Instance));
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(s => s.Endpoints)
            .OfType<RouteEndpoint>().ToArray();
        var completed = new CompletionFeature();
        var ctx = Context(app, 5180, completed);
        ctx.Request.Headers[RuntimeIdentity.Header] = Key;
        ctx.Request.Headers["X-ArcFlow-Instance"] = Instance;
        ctx.Request.Headers["X-ArcFlow-Pid"] = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        switch (wrong)
        {
            case "remote": ctx.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.1"); break;
            case "host": ctx.Request.Host = new HostString("attacker.example", 5180); break;
            case "origin": ctx.Request.Headers.Origin = "http://127.0.0.1:5180"; break;
            case "key": ctx.Request.Headers[RuntimeIdentity.Header] = new string('f', 64); break;
            case "instance": ctx.Request.Headers["X-ArcFlow-Instance"] = new string('2', 32); break;
            case "pid": ctx.Request.Headers["X-ArcFlow-Pid"] = "0"; break;
            case "missing": ctx.Request.Headers.Remove(RuntimeIdentity.Header); break;
        }
        await Invoke(endpoints, "/_arcflow/shutdown", ctx);
        Assert.Equal(403, ctx.Response.StatusCode);
        await completed.CompleteAsync();
        Assert.False(app.Lifetime.ApplicationStopping.IsCancellationRequested);
        if (wrong is "remote" or "host" or "origin")
        {
            await Invoke(endpoints, "/_arcflow/identity", ctx);
            Assert.Equal(403, ctx.Response.StatusCode);
        }
    }

    [Theory]
    [InlineData("http://127.0.0.1:5180", "http://127.0.0.1:5180")]
    [InlineData("http://localhost:5180/", "http://127.0.0.1:5180")]
    [InlineData("https://localhost:5180", null)]
    [InlineData("http://localhost:5180/path", null)]
    [InlineData("http://localhost:5180/?x=1", null)]
    [InlineData("http://127.1:5180", null)]
    [InlineData("http://user@localhost:5180", null)]
    public void McpDependencyMetadataIsStrictlyCanonical(string input, string? expected) =>
        Assert.Equal(expected, new RuntimeIdentity("mcp", input, null, null).ApiBaseUrl);

    [Fact]
    public void IndependentInstanceHasNoOwnerShutdownCapability()
    {
        var independent = new RuntimeIdentity("api", null, null, null);
        Assert.True(Guid.TryParseExact(independent.InstanceId, "N", out _));
        Assert.False(independent.AuthorizeShutdown(Key, independent.InstanceId,
            Environment.ProcessId.ToString(CultureInfo.InvariantCulture)));
        Assert.False(independent.LocalRequest(null, "localhost:5180", null));
        Assert.False(independent.LocalRequest(IPAddress.Loopback, null, null));
    }

    private static DefaultHttpContext Context(WebApplication app, int port, CompletionFeature? completion = null)
    {
        var ctx = new DefaultHttpContext { RequestServices = app.Services };
        if (completion is not null) ctx.Features.Set<IHttpResponseFeature>(completion);
        ctx.Connection.RemoteIpAddress = IPAddress.Loopback;
        ctx.Request.Host = new HostString("127.0.0.1", port);
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    private static Task Invoke(RouteEndpoint[] endpoints, string path, DefaultHttpContext ctx)
    {
        var endpoint = endpoints.Single(e => e.RoutePattern.RawText == path);
        ctx.Request.Path = path;
        ctx.Request.Method = path.EndsWith("shutdown", StringComparison.Ordinal) ? "POST" : "GET";
        ctx.SetEndpoint(endpoint);
        return endpoint.RequestDelegate!(ctx);
    }

    private sealed class CompletionFeature : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _callbacks = new();
        public override void OnCompleted(Func<object, Task> callback, object state) => _callbacks.Add((callback, state));
        internal async Task CompleteAsync()
        {
            foreach (var (callback, state) in _callbacks.AsEnumerable().Reverse()) await callback(state);
        }
    }
}
