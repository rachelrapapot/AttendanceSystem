using AttendanceSystem.API.Filters;
using System.ComponentModel.DataAnnotations;

namespace AttendanceSystem.API.DTOs;

public record CreateEmployeeRequest(
    [Required][EmailAddress][MaxLength(256)] string Email,
    [Required][MinLength(8)][MaxLength(128)][PasswordComplexity] string Password,
    [Required][MaxLength(100)] string FirstName,
    [Required][MaxLength(100)] string LastName,
    [Required][AllowedValues("Employee", "Admin", ErrorMessage = "Role must be 'Employee' or 'Admin'.")] string Role
);

public record UpdateEmployeeRequest(
    [MaxLength(100)] string? FirstName,
    [MaxLength(100)] string? LastName,
    [MinLength(8)][MaxLength(128)][PasswordComplexity] string? Password,
    [AllowedValues("Active", "Inactive", "Terminated", ErrorMessage = "Status must be 'Active', 'Inactive', or 'Terminated'.")] string? Status
);

public record EmployeeResponse(
    int EmployeeId,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    string Status,
    DateTimeOffset CreatedAt
)
{
    public string FullName => $"{FirstName} {LastName}";
}

public record SessionEntry(
    DateTimeOffset ClockIn,
    DateTimeOffset? ClockOut,
    int MinutesWorked,
    bool IsComplete
);

public record DailyEntry(
    DateOnly Date,
    List<SessionEntry> Sessions,
    int TotalMinutesWorked,
    bool IsComplete
);

public record ReportEntry(
    int EmployeeId,
    string FullName,
    string Email,
    int TotalMinutesWorked,
    int TotalClockEvents,
    List<DailyEntry> DailyBreakdown
);

public record AuditLogResponse(
    long AuditId,
    int? EmployeeId,
    string Action,
    string? Details,
    DateTimeOffset Timestamp,
    DateTimeOffset CreatedAt
);

public record UpdateClockEventRequest(
    [Required] DateTimeOffset Timestamp
);
