using AttendanceSystem.API.Data;
using AttendanceSystem.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AttendanceSystem.Tests.Integration;

public class AuthTests : IAsyncLifetime
{
    private readonly TestFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        await TestSeeder.SeedAsync(_factory.Services);
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    // -------------------------------------------------------------------------
    // Login
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Login_WithCorrectCredentials_Returns200AndSetsCookies()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = TestSeeder.AdminEmail,
            password = TestSeeder.AdminPassword
        });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(TestSeeder.AdminEmail, body.GetProperty("email").GetString());
        Assert.Equal("Admin", body.GetProperty("role").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("fullName").GetString()));

        // Cookies must be set (httpOnly so they appear in Set-Cookie header)
        var setCookie = res.Headers.GetValues("Set-Cookie").ToList();
        Assert.Contains(setCookie, c => c.StartsWith("access_token="));
        Assert.Contains(setCookie, c => c.StartsWith("refresh_token="));
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = TestSeeder.AdminEmail,
            password = "wrongpassword"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "nobody@example.com",
            password = "somepassword"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_WithInactiveEmployee_Returns401()
    {
        // Deactivate employee first via direct DB
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emp = db.Employees.First(e => e.Email == TestSeeder.EmployeeEmail);
        emp.Status = "Inactive";
        await db.SaveChangesAsync();

        var res = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = TestSeeder.EmployeeEmail,
            password = TestSeeder.EmployeePassword
        });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_FailedAttempt_WritesAuditLog()
    {
        await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = TestSeeder.EmployeeEmail,
            password = "wrongpassword"
        });

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(db.AuditLogs.Any(a => a.Action == "LogoutFailed"));
    }

    // -------------------------------------------------------------------------
    // Auth/Me
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        // Fresh client with no cookies
        using var unauthClient = _factory.CreateClient();
        var res = await unauthClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Me_AfterLogin_ReturnsCorrectUser()
    {
        await LoginAsAsync(TestSeeder.EmployeeEmail, TestSeeder.EmployeePassword);

        var res = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(TestSeeder.EmployeeEmail, body.GetProperty("email").GetString());
        Assert.Equal("Employee", body.GetProperty("role").GetString());
        Assert.Equal("Test Employee", body.GetProperty("fullName").GetString());
    }

    // -------------------------------------------------------------------------
    // Role-based access
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AdminRoute_WithAdminRole_Returns200()
    {
        await LoginAsAsync(TestSeeder.AdminEmail, TestSeeder.AdminPassword);
        var res = await _client.GetAsync("/api/admin/employees");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task AdminRoute_WithEmployeeRole_Returns403()
    {
        await LoginAsAsync(TestSeeder.EmployeeEmail, TestSeeder.EmployeePassword);
        var res = await _client.GetAsync("/api/admin/employees");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Logout
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Logout_ClearsSessionAndProtectedRouteReturns401()
    {
        await LoginAsAsync(TestSeeder.EmployeeEmail, TestSeeder.EmployeePassword);

        // Confirm authenticated
        var meBefore = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meBefore.StatusCode);

        // Logout
        var logoutRes = await _client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logoutRes.StatusCode);

        // The Set-Cookie header should expire cookies
        var setCookie = logoutRes.Headers.GetValues("Set-Cookie").ToList();
        Assert.Contains(setCookie, c => c.Contains("access_token=;") || c.Contains("access_token= ;"));
    }

    // -------------------------------------------------------------------------
    // Refresh
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Refresh_WithValidRefreshToken_IssuesNewAccessToken()
    {
        await LoginAsAsync(TestSeeder.EmployeeEmail, TestSeeder.EmployeePassword);

        var res = await _client.PostAsync("/api/auth/refresh", null);
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        var setCookie = res.Headers.GetValues("Set-Cookie").ToList();
        Assert.Contains(setCookie, c => c.StartsWith("access_token="));
    }

    [Fact]
    public async Task Refresh_WithoutToken_Returns401()
    {
        using var unauthClient = _factory.CreateClient();
        var res = await unauthClient.PostAsync("/api/auth/refresh", null);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task LoginAsAsync(string email, string password)
    {
        var res = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        res.EnsureSuccessStatusCode();
    }
}
