using AttendanceSystem.API.Data;
using AttendanceSystem.API.DTOs;
using AttendanceSystem.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AttendanceSystem.API.Services;

public interface IAdminService
{
    Task<List<EmployeeResponse>> GetEmployeesAsync();
    Task<EmployeeResponse> CreateEmployeeAsync(CreateEmployeeRequest request, int adminId);
    Task<EmployeeResponse?> UpdateEmployeeAsync(int employeeId, UpdateEmployeeRequest request, int adminId);
    Task<bool> TerminateEmployeeAsync(int employeeId, int adminId);
    Task<int?> RevokeAllSessionsAsync(int employeeId, int adminId);
    Task<List<AuditLogResponse>> GetAuditLogsAsync();
}

public class AdminService : IAdminService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly ITimeService _timeService;

    public AdminService(AppDbContext db, IAuditService audit, ITimeService timeService)
    {
        _db = db;
        _audit = audit;
        _timeService = timeService;
    }

    public async Task<List<EmployeeResponse>> GetEmployeesAsync()
    {
        return await _db.Employees
            .Include(e => e.Role)
            .Select(e => new EmployeeResponse(
                e.EmployeeId, e.Email, e.FirstName, e.LastName,
                e.Role.RoleName, e.Status, e.CreatedAt))
            .ToListAsync();
    }

    public async Task<EmployeeResponse> CreateEmployeeAsync(CreateEmployeeRequest request, int adminId)
    {
        var now = await _timeService.GetZurichTimeAsync();

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == request.Role)
            ?? throw new ArgumentException($"Role '{request.Role}' not found");

        var employee = new Employee
        {
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            FirstName = request.FirstName,
            LastName = request.LastName,
            RoleId = role.RoleId,
            Status = "Active",
            CreatedAt = now
        };

        await using var tx = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync()
            : (IDbContextTransaction?)null;
        _db.Employees.Add(employee);
        await _audit.LogAsync(_db, "AdminAction", adminId,
            new { action = "createEmployee", email = request.Email, role = request.Role }, now);
        await _db.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();

        return new EmployeeResponse(
            employee.EmployeeId, employee.Email, employee.FirstName, employee.LastName,
            role.RoleName, employee.Status, employee.CreatedAt);
    }

    public async Task<EmployeeResponse?> UpdateEmployeeAsync(int employeeId, UpdateEmployeeRequest request, int adminId)
    {
        var now = await _timeService.GetZurichTimeAsync();
        var employee = await _db.Employees.Include(e => e.Role)
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
        if (employee == null) return null;

        await using var tx = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync()
            : (IDbContextTransaction?)null;

        if (request.FirstName != null) employee.FirstName = request.FirstName;
        if (request.LastName != null) employee.LastName = request.LastName;
        if (request.Password != null)
            employee.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);
        if (request.Status != null) employee.Status = request.Status;

        await _audit.LogAsync(_db, "AdminAction", adminId,
            new { action = "updateEmployee", employeeId, updatedFields = new { request.FirstName, request.LastName, hasNewPassword = request.Password != null, request.Status } },
            now);
        await _db.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();

        return new EmployeeResponse(
            employee.EmployeeId, employee.Email, employee.FirstName, employee.LastName,
            employee.Role.RoleName, employee.Status, employee.CreatedAt);
    }

    public async Task<bool> TerminateEmployeeAsync(int employeeId, int adminId)
    {
        var now = await _timeService.GetZurichTimeAsync();
        var employee = await _db.Employees
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId && e.Status != "Terminated");
        if (employee == null) return false;

        await using var tx = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync()
            : (IDbContextTransaction?)null;

        employee.Status = "Terminated";

        // Revoke all active refresh tokens atomically with the status change.
        // Without this, a valid JWT (up to 15 min) plus an un-revoked refresh token
        // could keep a terminated employee's session alive for up to 7 days.
        var activeTokens = await _db.RefreshTokens
            .Where(rt => rt.EmployeeId == employeeId && rt.RevokedAt == null)
            .ToListAsync();
        foreach (var t in activeTokens)
            t.RevokedAt = now;

        await _audit.LogAsync(_db, "AdminAction", adminId,
            new { action = "terminateEmployee", employeeId, sessionsRevoked = activeTokens.Count }, now);
        await _db.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();

        return true;
    }

    public async Task<int?> RevokeAllSessionsAsync(int employeeId, int adminId)
    {
        var now = await _timeService.GetZurichTimeAsync();

        var exists = await _db.Employees.AnyAsync(e => e.EmployeeId == employeeId);
        if (!exists) return null;

        var activeTokens = await _db.RefreshTokens
            .Where(rt => rt.EmployeeId == employeeId && rt.RevokedAt == null)
            .ToListAsync();
        foreach (var t in activeTokens)
            t.RevokedAt = now;

        await _audit.LogAsync(_db, "AdminAction", adminId,
            new { action = "revokeAllSessions", employeeId, sessionsRevoked = activeTokens.Count }, now);
        await _db.SaveChangesAsync();

        return activeTokens.Count;
    }

    public async Task<List<AuditLogResponse>> GetAuditLogsAsync()
    {
        return await _db.AuditLogs
            .OrderByDescending(al => al.Timestamp)
            .Select(al => new AuditLogResponse(
                al.AuditId, al.EmployeeId, al.Action, al.Details, al.Timestamp, al.CreatedAt))
            .ToListAsync();
    }
}
