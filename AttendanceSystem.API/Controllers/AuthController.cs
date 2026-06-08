using AttendanceSystem.API.DTOs;
using AttendanceSystem.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AttendanceSystem.API.Controllers;

/// <summary>Handles authentication: login, logout, token refresh, and profile retrieval.</summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController(IAuthService authService, IWebHostEnvironment env) : ControllerBase
{
    private const string AccessTokenCookie = "access_token";
    private const string RefreshTokenCookie = "refresh_token";

    /// <summary>
    /// Authenticates an employee with email and password.
    /// </summary>
    /// <remarks>
    /// On success, sets two httpOnly cookies:
    /// - <c>access_token</c> — JWT valid for 15 minutes, used to authenticate API calls.
    /// - <c>refresh_token</c> — opaque token valid for 7 days, used to renew the access token.
    ///
    /// Both cookies are HttpOnly and SameSite=Strict, making them inaccessible to JavaScript
    /// and immune to cross-site request forgery. After calling this endpoint in Swagger UI,
    /// the browser holds the cookies and subsequent Swagger requests authenticate automatically.
    /// </remarks>
    /// <param name="request">Employee email and password.</param>
    /// <response code="200">Authentication succeeded. Returns employee profile; auth cookies are set.</response>
    /// <response code="400">Request body is malformed or missing required fields.</response>
    /// <response code="401">Email/password are incorrect, or the account is inactive.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var ip = GetClientIp();
        var result = await authService.LoginAsync(request, ip);

        if (result == null)
            return Unauthorized(new { error = "Invalid email or password." });

        var (employee, accessToken, refreshToken) = result.Value;

        SetCookie(AccessTokenCookie, accessToken, DateTime.UtcNow.AddMinutes(15));
        SetCookie(RefreshTokenCookie, refreshToken, DateTime.UtcNow.AddDays(7));

        return Ok(new LoginResponse(
            employee.EmployeeId, employee.FirstName, employee.LastName,
            employee.Email, employee.Role.RoleName));
    }

    /// <summary>
    /// Revokes the current refresh token and clears all auth cookies.
    /// </summary>
    /// <remarks>
    /// The refresh token is invalidated in the database, preventing it from being used to
    /// issue new access tokens. The access token is stateless and cannot be revoked, but it
    /// expires within 15 minutes regardless. Both cookies are cleared from the browser.
    /// </remarks>
    /// <response code="204">Logout successful. Auth cookies are cleared.</response>
    /// <response code="401">No valid <c>access_token</c> cookie present.</response>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        var rawRefresh = Request.Cookies[RefreshTokenCookie];
        if (rawRefresh != null)
        {
            var employeeId = GetEmployeeId();
            await authService.LogoutAsync(employeeId, rawRefresh);
        }

        ClearCookie(AccessTokenCookie);
        ClearCookie(RefreshTokenCookie);

        return NoContent();
    }

    /// <summary>
    /// Exchanges the refresh token cookie for a new access token and refresh token pair.
    /// </summary>
    /// <remarks>
    /// The old refresh token is consumed and replaced; clients cannot reuse it after this call.
    /// New <c>access_token</c> (15 min) and <c>refresh_token</c> (7 days) cookies are issued.
    /// If the refresh token is invalid or expired, both cookies are cleared and the client
    /// must re-authenticate via <c>POST /api/auth/login</c>.
    /// </remarks>
    /// <response code="204">Tokens refreshed. New access_token and refresh_token cookies are set.</response>
    /// <response code="401">Refresh token cookie is missing, expired, or has been revoked.</response>
    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh()
    {
        var rawRefresh = Request.Cookies[RefreshTokenCookie];
        if (string.IsNullOrEmpty(rawRefresh))
            return Unauthorized(new { error = "No refresh token." });

        var ip = GetClientIp();
        var result = await authService.RefreshAsync(rawRefresh, ip);

        if (result == null)
        {
            ClearCookie(AccessTokenCookie);
            ClearCookie(RefreshTokenCookie);
            return Unauthorized(new { error = "Refresh token invalid or expired." });
        }

        var (newAccess, newRefresh) = result.Value;
        SetCookie(AccessTokenCookie, newAccess, DateTime.UtcNow.AddMinutes(15));
        SetCookie(RefreshTokenCookie, newRefresh, DateTime.UtcNow.AddDays(7));

        return NoContent();
    }

    /// <summary>
    /// Returns the profile of the currently authenticated employee.
    /// </summary>
    /// <response code="200">Returns the authenticated employee's id, name, email, and role.</response>
    /// <response code="401">No valid <c>access_token</c> cookie, or the token subject does not match an active employee.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MeResponse>> Me()
    {
        var employeeId = GetEmployeeId();
        var employee = await authService.GetEmployeeByIdAsync(employeeId);
        if (employee == null) return Unauthorized();

        return Ok(new MeResponse(
            employee.EmployeeId, employee.FirstName, employee.LastName,
            employee.Email, employee.Role.RoleName));
    }

    private void SetCookie(string name, string value, DateTime expires)
    {
        Response.Cookies.Append(name, value, new CookieOptions
        {
            HttpOnly = true,
            Secure = !env.IsDevelopment(), // HTTP proxy in dev; HTTPS required in production
            SameSite = SameSiteMode.Strict,
            Expires = expires,
            Path = "/"
        });
    }

    private void ClearCookie(string name)
    {
        Response.Cookies.Append(name, "", new CookieOptions
        {
            HttpOnly = true,
            Secure = !env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(-1),
            Path = "/"
        });
    }

    private int GetEmployeeId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new InvalidOperationException("No employee ID in token");
        return int.Parse(sub);
    }

    private string GetClientIp() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
