namespace AttendanceSystem.API.DTOs;

public record ClockEventResponse(
    int EventId,
    int EmployeeId,
    string EmployeeName,
    string EventType,
    DateTimeOffset Timestamp
);

public record ClockStatusLastEvent(int EventId, string EventType, DateTimeOffset Timestamp);

public record ClockStatusResponse(bool IsClockedIn, ClockStatusLastEvent? LastEvent);
