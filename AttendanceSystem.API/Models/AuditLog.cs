namespace AttendanceSystem.API.Models;

public class AuditLog
{
    public long AuditId { get; set; }
    public int? EmployeeId { get; set; }
    public string Action { get; set; } = string.Empty;  // ClockIn, ClockOut, Login, LogoutFailed, AdminAction
    public string? Details { get; set; }  // JSON
    public DateTimeOffset Timestamp { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Employee? Employee { get; set; }
}
