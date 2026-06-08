using AttendanceSystem.API.Data;
using AttendanceSystem.API.Models;
using AttendanceSystem.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AttendanceSystem.Tests.Integration;

public class ClockTests : IAsyncLifetime
{
    private readonly TestFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        await TestSeeder.SeedAsync(_factory.Services);
        await LoginAsEmployeeAsync();
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    // -------------------------------------------------------------------------
    // Clock In
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ClockIn_Returns200WithEventDetails()
    {
        var res = await _client.PostAsync("/api/clock/in", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ClockIn", body.GetProperty("eventType").GetString());
        Assert.True(body.GetProperty("employeeId").GetInt32() > 0);
    }

    [Fact]
    public async Task ClockIn_CreatesAuditLog()
    {
        await _client.PostAsync("/api/clock/in", null);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(db.AuditLogs.Any(a => a.Action == "ClockIn"));
    }

    [Fact]
    public async Task ClockIn_WhenAlreadyClockedIn_Returns400()
    {
        await _client.PostAsync("/api/clock/in", null); // First clock-in
        var res = await _client.PostAsync("/api/clock/in", null); // Duplicate
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task ClockIn_AfterClockOut_Returns200()
    {
        await _client.PostAsync("/api/clock/in", null);
        await _client.PostAsync("/api/clock/out", null);
        var res = await _client.PostAsync("/api/clock/in", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task MultipleClockInOut_RecordsAllFourEvents()
    {
        await _client.PostAsync("/api/clock/in", null);
        await _client.PostAsync("/api/clock/out", null);
        await _client.PostAsync("/api/clock/in", null);
        await _client.PostAsync("/api/clock/out", null);

        var res = await _client.GetAsync("/api/clock/history");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(4, body.GetArrayLength());
    }

    // -------------------------------------------------------------------------
    // Clock Out
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ClockOut_AfterClockIn_Returns200()
    {
        await _client.PostAsync("/api/clock/in", null);
        var res = await _client.PostAsync("/api/clock/out", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ClockOut", body.GetProperty("eventType").GetString());
    }

    [Fact]
    public async Task ClockOut_WithoutClockIn_Returns400()
    {
        var res = await _client.PostAsync("/api/clock/out", null);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task ClockOut_WhenAlreadyClockedOut_Returns400()
    {
        await _client.PostAsync("/api/clock/in", null);
        await _client.PostAsync("/api/clock/out", null);
        var res = await _client.PostAsync("/api/clock/out", null);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Status
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Status_WhenNeverClocked_ReturnsFalseAndNullLastEvent()
    {
        var res = await _client.GetAsync("/api/clock/status");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("isClockedIn").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("lastEvent").ValueKind);
    }

    [Fact]
    public async Task Status_AfterClockIn_ReturnsTrueWithLastEvent()
    {
        await _client.PostAsync("/api/clock/in", null);

        var res = await _client.GetAsync("/api/clock/status");
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.GetProperty("isClockedIn").GetBoolean());
        var lastEvent = body.GetProperty("lastEvent");
        Assert.Equal("ClockIn", lastEvent.GetProperty("eventType").GetString());
    }

    [Fact]
    public async Task Status_AfterClockInAndOut_ReturnsFalseWithClockOutEvent()
    {
        await _client.PostAsync("/api/clock/in", null);
        await _client.PostAsync("/api/clock/out", null);

        var res = await _client.GetAsync("/api/clock/status");
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(body.GetProperty("isClockedIn").GetBoolean());
        Assert.Equal("ClockOut", body.GetProperty("lastEvent").GetProperty("eventType").GetString());
    }

    // -------------------------------------------------------------------------
    // Timezone accuracy
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ClockIn_TimestampMatchesZurichTime()
    {
        var res = await _client.PostAsync("/api/clock/in", null);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var ts = DateTimeOffset.Parse(body.GetProperty("timestamp").GetString()!);
        // Timestamp must match the mocked Zurich time
        Assert.Equal(TestFactory.FixedZurichTime.UtcDateTime, ts.UtcDateTime);
    }

    // -------------------------------------------------------------------------
    // Unauthenticated access
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ClockIn_WithoutToken_Returns401()
    {
        using var unauthClient = _factory.CreateClient();
        var res = await unauthClient.PostAsync("/api/clock/in", null);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task LoginAsEmployeeAsync()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = TestSeeder.EmployeeEmail,
            password = TestSeeder.EmployeePassword
        });
        res.EnsureSuccessStatusCode();
    }
}
