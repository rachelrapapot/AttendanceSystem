using AttendanceSystem.API.Data;
using AttendanceSystem.API.DTOs;
using AttendanceSystem.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AttendanceSystem.API.Services;

public interface IAuthService
{
    Task<(Employee Employee, string AccessToken, string RefreshToken)?> LoginAsync(LoginRequest request, string ipAddress);
    Task<(string AccessToken, string RefreshToken)?> RefreshAsync(string rawRefreshToken, string ipAddress);
    Task<bool> LogoutAsync(int employeeId, string rawRefreshToken);
    Task<Employee?> GetEmployeeByIdAsync(int id);
}

public class AuthService : IAuthService
{
    private readonly AppDbContext  _db;
    private readonly ITokenService _tokenService;
    private readonly IAuditService _audit;
    private readonly ITimeService  _timeService;
    private readonly AppMetrics    _metrics;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AppDbContext db, ITokenService tokenService, IAuditService audit,
        ITimeService timeService, AppMetrics metrics, ILogger<AuthService> logger)
    {
        _db           = db;
        _tokenService = tokenService;
        _audit        = audit;
        _timeService  = timeService;
        _metrics      = metrics;
        _logger       = logger;
    }

    public async Task<(Employee Employee, string AccessToken, string RefreshToken)?> LoginAsync(LoginRequest request, string ipAddress)
    {
        _logger.LogInformation("[AUTH] Login attempt for {Email} from {IP}", request.Email, ipAddress);

        var now = await _timeService.GetZurichTimeAsync();

        var employee = await _db.Employees
            .Include(e => e.Role)
            .FirstOrDefaultAsync(e => e.Email == request.Email && e.Status == "Active");

        if (employee == null || !BCrypt.Net.BCrypt.Verify(request.Password, employee.PasswordHash))
        {
            _metrics.LoginFailures.Add(1);
            _logger.LogWarning("[AUTH] Failed login for {Email} from {IP}", request.Email, ipAddress);

            _db.AuditLogs.Add(new AuditLog
            {
                Action    = "LogoutFailed",
                Details   = JsonSerializer.Serialize(new { email = request.Email, ip = ipAddress }),
                Timestamp = now,
                CreatedAt = now
            });
            await _db.SaveChangesAsync();

            await AlertOnBruteForceAsync(request.Email, ipAddress, now);

            return null;
        }

        var rawRefresh  = _tokenService.GenerateRawRefreshToken();
        var refreshHash = _tokenService.HashToken(rawRefresh);

        var refreshToken = new RefreshToken
        {
            EmployeeId = employee.EmployeeId,
            TokenHash  = refreshHash,
            ExpiresAt  = now.AddDays(7),
            CreatedAt  = now
        };

        _db.RefreshTokens.Add(refreshToken);
        await _audit.LogAsync(_db, "Login", employee.EmployeeId, new { ip = ipAddress }, now);
        await _db.SaveChangesAsync();

        _logger.LogInformation("[AUTH] Successful login — EmployeeId={EmployeeId} Role={Role} IP={IP}",
            employee.EmployeeId, employee.Role.RoleName, ipAddress);

        return (employee, _tokenService.GenerateAccessToken(employee), rawRefresh);
    }

    private async Task AlertOnBruteForceAsync(string email, string ipAddress, DateTimeOffset now)
    {
        // Count failures for this email in the last 15 minutes (same window as the rate limiter)
        var recentEmailFailures = await _db.AuditLogs
            .CountAsync(al => al.Action    == "LogoutFailed"
                           && al.Timestamp >= now.AddMinutes(-15)
                           && al.Details   != null
                           && al.Details.Contains(email));

        if (recentEmailFailures >= 5)
            _logger.LogCritical(
                "[SECURITY ALERT] {Count} failed logins for {Email} in 15 min — possible brute-force (latest from {IP})",
                recentEmailFailures, email, ipAddress);

        // Alert when the same IP hammers many different accounts (credential stuffing)
        var recentIpFailures = await _db.AuditLogs
            .CountAsync(al => al.Action    == "LogoutFailed"
                           && al.Timestamp >= now.AddMinutes(-15)
                           && al.Details   != null
                           && al.Details.Contains(ipAddress));

        if (recentIpFailures >= 10)
            _logger.LogCritical(
                "[SECURITY ALERT] {Count} failed logins from {IP} in 15 min — possible credential stuffing",
                recentIpFailures, ipAddress);
    }

    public async Task<(string AccessToken, string RefreshToken)?> RefreshAsync(string rawRefreshToken, string ipAddress)
    {
        var now  = await _timeService.GetZurichTimeAsync();
        var hash = _tokenService.HashToken(rawRefreshToken);

        var stored = await _db.RefreshTokens
            .Include(rt => rt.Employee).ThenInclude(e => e.Role)
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash);

        if (stored == null)
        {
            _logger.LogWarning("[AUTH] Refresh token not found from {IP}", ipAddress);
            return null;
        }

        // Stolen token detection: replaying a revoked token revokes ALL tokens for this employee
        if (stored.RevokedAt != null)
        {
            _logger.LogCritical(
                "[SECURITY ALERT] Revoked refresh token replayed — EmployeeId={Id} IP={IP} — revoking all sessions",
                stored.EmployeeId, ipAddress);

            var allTokens = await _db.RefreshTokens
                .Where(rt => rt.EmployeeId == stored.EmployeeId && rt.RevokedAt == null)
                .ToListAsync();
            foreach (var t in allTokens) t.RevokedAt = now;

            await _audit.LogAsync(_db, "LogoutFailed", stored.EmployeeId,
                new { reason = "revokedTokenReplay", ip = ipAddress }, now);
            await _db.SaveChangesAsync();
            return null;
        }

        if (stored.ExpiresAt < now)
        {
            stored.RevokedAt = now;
            await _db.SaveChangesAsync();
            _logger.LogInformation("[AUTH] Refresh token expired for EmployeeId={Id}", stored.EmployeeId);
            return null;
        }

        if (stored.Employee.Status != "Active")
        {
            _logger.LogWarning("[AUTH] Refresh blocked — account is {Status} (EmployeeId={Id})",
                stored.Employee.Status, stored.EmployeeId);
            return null;
        }

        // Rotate: revoke old, issue new
        stored.RevokedAt = now;
        var newRaw  = _tokenService.GenerateRawRefreshToken();
        var newHash = _tokenService.HashToken(newRaw);

        _db.RefreshTokens.Add(new RefreshToken
        {
            EmployeeId = stored.EmployeeId,
            TokenHash  = newHash,
            ExpiresAt  = now.AddDays(7),
            CreatedAt  = now
        });

        await _audit.LogAsync(_db, "Login", stored.EmployeeId,
            new { action = "tokenRefreshed", ip = ipAddress }, now);
        await _db.SaveChangesAsync();

        _logger.LogInformation("[AUTH] Token refreshed — EmployeeId={Id} IP={IP}", stored.EmployeeId, ipAddress);

        return (_tokenService.GenerateAccessToken(stored.Employee), newRaw);
    }

    public async Task<bool> LogoutAsync(int employeeId, string rawRefreshToken)
    {
        var now   = await _timeService.GetZurichTimeAsync();
        var hash  = _tokenService.HashToken(rawRefreshToken);
        var token = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.EmployeeId == employeeId && rt.TokenHash == hash && rt.RevokedAt == null);

        if (token == null) return false;

        token.RevokedAt = now;
        await _db.SaveChangesAsync();

        _logger.LogInformation("[AUTH] Logout — EmployeeId={Id}", employeeId);
        return true;
    }

    public Task<Employee?> GetEmployeeByIdAsync(int id) =>
        _db.Employees.Include(e => e.Role).FirstOrDefaultAsync(e => e.EmployeeId == id && e.Status == "Active");
}
