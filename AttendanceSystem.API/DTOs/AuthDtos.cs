using System.ComponentModel.DataAnnotations;

namespace AttendanceSystem.API.DTOs;

public record LoginRequest(
    [Required][EmailAddress][MaxLength(256)] string Email,
    [Required][MinLength(8)][MaxLength(128)] string Password
);

public record LoginResponse(int EmployeeId, string FirstName, string LastName, string Email, string Role)
{
    public string FullName => $"{FirstName} {LastName}";
}

public record MeResponse(int EmployeeId, string FirstName, string LastName, string Email, string Role)
{
    public string FullName => $"{FirstName} {LastName}";
}
