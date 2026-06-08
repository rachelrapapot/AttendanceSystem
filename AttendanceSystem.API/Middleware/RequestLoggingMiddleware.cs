using AttendanceSystem.API.Services;
using System.Diagnostics;

namespace AttendanceSystem.API.Middleware;

public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger, AppMetrics metrics)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            sw.Stop();
            var status = context.Response.StatusCode;
            var method = context.Request.Method;
            var path   = context.Request.Path.Value ?? "/";
            var ip     = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            metrics.RequestDurationMs.Record(sw.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("http.method",      method),
                new KeyValuePair<string, object?>("http.status_code", status));

            var level = status >= 500 ? LogLevel.Error
                      : status >= 400 ? LogLevel.Warning
                      : LogLevel.Information;

            logger.Log(level, "[HTTP] {Method} {Path} → {Status} ({Ms:F0}ms) from {IP}",
                method, path, status, sw.Elapsed.TotalMilliseconds, ip);

            if (status == 401)
                logger.LogWarning("[AUTHN] Unauthenticated request rejected: {Method} {Path} from {IP}", method, path, ip);
            else if (status == 403)
                logger.LogWarning("[AUTHZ] Authorization denied: {Method} {Path} from {IP}", method, path, ip);
        }
    }
}
