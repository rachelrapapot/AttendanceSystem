namespace AttendanceSystem.API.Models;

public class RefreshToken
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Employee Employee { get; set; } = null!;
}
