using AttendanceSystem.API.Data;
using AttendanceSystem.API.Models;
using System.Text.Json;

namespace AttendanceSystem.API.Services;

public interface IAuditService
{
    Task LogAsync(AppDbContext db, string action, int? employeeId, object? details, DateTimeOffset timestamp);
}

public class AuditService : IAuditService
{
    public Task LogAsync(AppDbContext db, string action, int? employeeId, object? details, DateTimeOffset timestamp)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EmployeeId = employeeId,
            Details = details != null ? JsonSerializer.Serialize(details) : null,
            Timestamp = timestamp,
            CreatedAt = timestamp
        });
        return Task.CompletedTask;
    }
}
