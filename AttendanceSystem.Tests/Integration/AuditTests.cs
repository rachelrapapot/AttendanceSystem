using AttendanceSystem.API.Data;
using AttendanceSystem.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace AttendanceSystem.Tests.Integration;

/// <summary>
/// Verifies audit trail completeness: every state-changing action must produce
/// an AuditLog entry in the same transaction.
/// </summary>
public class AuditTests : IAsyncLifetime
{
    private readonly TestFactory _factory = new();
    private HttpClient _adminClient = null!;
    private HttpClient _employeeClient = null!;

    public async Task InitializeAsync()
    {
        _adminClient = _factory.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        _employeeClient = _factory.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        await TestSeeder.SeedAsync(_factory.Services);
        await LoginAsync(_adminClient, TestSeeder.AdminEmail, TestSeeder.AdminPassword);
        await LoginAsync(_employeeClient, TestSeeder.EmployeeEmail, TestSeeder.EmployeePassword);
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task SuccessfulLogin_WritesLoginAuditLog()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(db.AuditLogs.Any(a => a.Action == "Login"));
    }

    [Fact]
    public async Task FailedLogin_WritesLogoutFailedAuditLog()
    {
        using var freshClient = _factory.CreateClient();
        await freshClient.PostAsJsonAsync("/api/auth/login", new
        {
            email = "nobody@test.com",
            password = "wrongpassword"
        });

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(db.AuditLogs.Any(a => a.Action == "LogoutFailed"));
    }

    [Fact]
    public async Task ClockIn_WritesClockInAuditLog()
    {
        await _employeeClient.PostAsync("/api/clock/in", null);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(db.AuditLogs.Any(a => a.Action == "ClockIn"));
    }

    [Fact]
    public async Task ClockOut_WritesClockOutAuditLog()
    {
        await _employeeClient.PostAsync("/api/clock/in", null);
        await _employeeClient.PostAsync("/api/clock/out", null);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(db.AuditLogs.Any(a => a.Action == "ClockOut"));
    }

    [Fact]
    public async Task CreateEmployee_AdminAction_WritesAdminActionAuditLog()
    {
        await _adminClient.PostAsJsonAsync("/api/admin/employees", new
        {
            email = "newemployee@test.com",
            password = "NewPass123!",
            firstName = "New",
            lastName = "Person",
            role = "Employee"
        });

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(db.AuditLogs.Any(a => a.Action == "AdminAction"));
    }

    [Fact]
    public async Task AuditLogsEndpoint_AdminCanRetrieveAllLogs()
    {
        // Ensure there's at least 1 log (from login)
        var res = await _adminClient.GetAsync("/api/admin/audit-logs");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var logs = await res.Content.ReadFromJsonAsync<List<System.Text.Json.JsonElement>>();
        Assert.NotNull(logs);
        Assert.True(logs.Count > 0);
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        var res = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        res.EnsureSuccessStatusCode();
    }
}
