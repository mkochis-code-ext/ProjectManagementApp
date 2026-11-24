using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ProjectManagementApp;

public static class RuntimeEndpoints
{
    public static void MapArcFlowRuntime(this WebApplication app, RuntimeIdentity runtime)
    {
        app.MapGet("/_arcflow/identity", Task<IResult> (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Task.FromResult<IResult>(runtime.LocalRequest(context.Connection.RemoteIpAddress,
                context.Request.Host.Value, context.Request.Headers.Origin)
                ? Results.Json(runtime) : Results.StatusCode(403));
        });
        app.MapPost("/_arcflow/shutdown", Task<IResult> (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!runtime.LocalRequest(context.Connection.RemoteIpAddress, context.Request.Host.Value,
                    context.Request.Headers.Origin) ||
                !runtime.AuthorizeShutdown(context.Request.Headers[RuntimeIdentity.Header],
                    context.Request.Headers["X-ArcFlow-Instance"], context.Request.Headers["X-ArcFlow-Pid"]))
                return Task.FromResult<IResult>(Results.StatusCode(403));
            context.Response.OnCompleted(() => { app.Lifetime.StopApplication(); return Task.CompletedTask; });
            return Task.FromResult<IResult>(Results.NoContent());
        });
    }
}
