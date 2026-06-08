namespace AttendanceSystem.API.Models;

public class ClockEvent
{
    public int EventId { get; set; }
    public int EmployeeId { get; set; }
    public string EventType { get; set; } = string.Empty;  // ClockIn, ClockOut
    public DateTimeOffset Timestamp { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Employee Employee { get; set; } = null!;
}
