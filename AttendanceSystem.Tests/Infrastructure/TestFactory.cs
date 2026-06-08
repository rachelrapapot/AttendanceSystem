using AttendanceSystem.API.Data;
using AttendanceSystem.API.Models;
using AttendanceSystem.API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AttendanceSystem.Tests.Infrastructure;

/// <summary>
/// Custom factory that replaces SQL Server with InMemory EF and mocks ITimeService.
/// </summary>
public class TestFactory : WebApplicationFactory<Program>
{
    public static readonly DateTimeOffset FixedZurichTime =
        new DateTimeOffset(2026, 6, 7, 9, 0, 0, TimeSpan.FromHours(2)); // Europe/Zurich CEST

    public Mock<ITimeService> TimeServiceMock { get; } = new();

    public TestFactory()
    {
        // Default: return the fixed Zurich time
        TimeServiceMock.Setup(t => t.GetZurichTimeAsync())
                       .ReturnsAsync(FixedZurichTime);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Override seed settings so SeedData.InitializeAsync uses test credentials
        builder.UseSetting("Seed:AdminEmail", TestSeeder.AdminEmail);
        builder.UseSetting("Seed:AdminPassword", TestSeeder.AdminPassword);

        builder.ConfigureServices(services =>
        {
            // Remove real DbContext registration
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDescriptor != null) services.Remove(dbDescriptor);

            // Unique database name captured once — NOT inside the lambda (would regenerate per scope)
            var dbName = Guid.NewGuid().ToString();
            services.AddDbContext<AppDbContext>(opts =>
                opts.UseInMemoryDatabase(dbName));

            // Replace real TimeService with the mock
            var timeDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(ITimeService));
            if (timeDescriptor != null) services.Remove(timeDescriptor);

            var httpClientDescriptor = services.SingleOrDefault(
                d => d.ImplementationType == typeof(TimeService));
            if (httpClientDescriptor != null) services.Remove(httpClientDescriptor);

            services.AddScoped<ITimeService>(_ => TimeServiceMock.Object);
        });
    }
}

/// <summary>
/// Seeds a fully isolated InMemory database with test data.
/// InMemory EF does NOT apply HasData via EnsureCreatedAsync, so everything is seeded here.
/// Safe to call multiple times (all inserts are conditional).
/// </summary>
public static class TestSeeder
{
    public const string AdminEmail = "admin@test.com";
    public const string AdminPassword = "Admin123!";
    public const string EmployeeEmail = "employee@test.com";
    public const string EmployeePassword = "Employee123!";

    public static async Task SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Roles + Admin are seeded by SeedData.InitializeAsync at startup.
        // Add the test Employee if not present.
        if (!await db.Employees.AnyAsync(e => e.Email == EmployeeEmail))
        {
            var employeeRole = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == "Employee")
                ?? throw new InvalidOperationException("Employee role not found — SeedData.InitializeAsync must run first.");

            db.Employees.Add(new Employee
            {
                Email = EmployeeEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(EmployeePassword, workFactor: 4),
                FirstName = "Test",
                LastName = "Employee",
                RoleId = employeeRole.RoleId,
                Status = "Active",
                CreatedAt = TestFactory.FixedZurichTime.AddDays(-30)
            });
            await db.SaveChangesAsync();
        }
    }
}
