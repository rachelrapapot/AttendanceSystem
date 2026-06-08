namespace AttendanceSystem.API.Models;

public class Employee
{
    public int EmployeeId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public string Status { get; set; } = "Active";  // Active, Inactive, Terminated
    public DateTimeOffset CreatedAt { get; set; }

    public Role Role { get; set; } = null!;
    public ICollection<ClockEvent> ClockEvents { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<AuditLog> AuditLogs { get; set; } = [];

    public string FullName => $"{FirstName} {LastName}";
}
