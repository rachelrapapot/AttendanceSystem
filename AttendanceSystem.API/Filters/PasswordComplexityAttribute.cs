using System.ComponentModel.DataAnnotations;

namespace AttendanceSystem.API.Filters;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public class PasswordComplexityAttribute : ValidationAttribute
{
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "password1", "password123", "password1!", "passw0rd",
        "123456", "12345678", "123456789", "1234567890",
        "qwerty", "qwerty123", "qwerty1",
        "abc123", "abc1234",
        "letmein", "letmein1",
        "monkey", "monkey1",
        "dragon", "master",
        "iloveyou", "trustno1",
        "sunshine", "welcome",
        "shadow", "superman",
        "michael", "football",
        "baseball", "111111",
        "Admin123", "Admin123!",
        "changeme", "changeme1",
        "test1234", "Test1234"
    };

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string password)
            return ValidationResult.Success; // null is handled by [Required] if needed

        if (!password.Any(char.IsUpper))
            return new ValidationResult("Password must contain at least one uppercase letter.");

        if (!password.Any(char.IsLower))
            return new ValidationResult("Password must contain at least one lowercase letter.");

        if (!password.Any(char.IsDigit))
            return new ValidationResult("Password must contain at least one number.");

        if (CommonPasswords.Contains(password))
            return new ValidationResult("Password is too common. Please choose a different password.");

        return ValidationResult.Success;
    }
}
