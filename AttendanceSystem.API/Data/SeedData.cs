using AttendanceSystem.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.API.Data;

public static class SeedData
{
    /// <summary>
    /// Ensures the database is created and seeded with an initial Admin user.
    /// Only runs if no Admin-role employee exists. Safe to call on every startup.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        // MigrateAsync is relational-only; EnsureCreatedAsync handles InMemory (tests)
        if (db.Database.IsRelational())
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();

        // HasData seed is not applied by EnsureCreatedAsync for non-relational providers (InMemory).
        // Seed roles manually if they're missing.
        if (!await db.Roles.AnyAsync())
        {
            db.Roles.AddRange(
                new Role { RoleId = 1, RoleName = "Employee" },
                new Role { RoleId = 2, RoleName = "Admin" }
            );
            await db.SaveChangesAsync();
        }

        var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == "Admin");
        if (adminRole == null)
        {
            logger.LogError("Admin role not found in Roles table — migration may not have run.");
            return;
        }

        var adminExists = await db.Employees.AnyAsync(e => e.RoleId == adminRole.RoleId);
        if (adminExists) return;

        var email = config["Seed:AdminEmail"]
            ?? throw new InvalidOperationException("Seed:AdminEmail not configured.");
        var password = config["Seed:AdminPassword"]
            ?? throw new InvalidOperationException("Seed:AdminPassword not configured.");

        var now = DateTimeOffset.UtcNow;
        db.Employees.Add(new Employee
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
            FirstName = "System",
            LastName = "Admin",
            RoleId = adminRole.RoleId,
            Status = "Active",
            CreatedAt = now
        });

        await db.SaveChangesAsync();
        logger.LogInformation("Seeded initial admin user: {Email}", email);
    }
}
