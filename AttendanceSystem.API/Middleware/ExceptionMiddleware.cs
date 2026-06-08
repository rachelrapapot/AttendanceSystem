using AttendanceSystem.API.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AttendanceSystem.API.Middleware;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IWebHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (TimeApiUnavailableException ex)
        {
            logger.LogError(ex, "[503] TimeAPI unavailable for {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteJson(context, StatusCodes.Status503ServiceUnavailable, ex.Message);
        }
        catch (AccountInactiveException ex)
        {
            // Account deactivated/terminated after token was issued — block the action with 403
            logger.LogWarning("[403] Account inactive: {Message} — EmployeeId={Id}",
                ex.Message, context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
            await WriteJson(context, StatusCodes.Status403Forbidden, ex.Message);
        }
        catch (DbUpdateException ex)
        {
            // Unique-constraint violation from concurrent clock-in/out that slipped past the app-level check
            logger.LogCritical(ex, "[DB] Constraint violation on {Method} {Path} — possible concurrent request",
                context.Request.Method, context.Request.Path);
            await WriteJson(context, StatusCodes.Status409Conflict,
                "A conflicting record already exists. Please refresh and try again.");
        }
        catch (InvalidOperationException ex)
        {
            await WriteJson(context, StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (ArgumentException ex)
        {
            await WriteJson(context, StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[500] Unhandled exception on {Method} {Path}",
                context.Request.Method, context.Request.Path);
            var message = env.IsDevelopment()
                ? $"{ex.GetType().Name}: {ex.Message}"
                : "An unexpected error occurred.";
            await WriteJson(context, StatusCodes.Status500InternalServerError, message);
        }
    }

    private static Task WriteJson(HttpContext context, int statusCode, string error)
    {
        context.Response.StatusCode  = statusCode;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new { error }));
    }
}
