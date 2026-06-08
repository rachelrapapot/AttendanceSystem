namespace AttendanceSystem.API.Middleware;

public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

        // Swagger UI requires inline scripts/styles and same-origin fetches for its JSON spec.
        // All other routes are API-only and need nothing beyond the response body itself.
        context.Response.Headers["Content-Security-Policy"] =
            context.Request.Path.StartsWithSegments("/swagger")
                ? "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:;"
                : "default-src 'none'";

        await next(context);
    }
}
