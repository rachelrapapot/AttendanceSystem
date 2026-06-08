using AttendanceSystem.API.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;

namespace AttendanceSystem.Tests.Unit;

/// <summary>
/// Unit tests for ITimeService retry logic and circuit breaker behaviour.
/// Uses a custom delegating handler to simulate API responses without hitting the network.
/// </summary>
public class TimeServiceTests
{
    private static TimeService BuildService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        var logger  = new Mock<ILogger<TimeService>>().Object;
        var metrics = new AppMetrics();
        return new TimeService(httpClient, logger, metrics);
    }

    [Fact]
    public async Task GetZurichTime_WhenApiRespondsOk_ReturnsParsedTime()
    {
        const string json = """
            {"dateTime":"2026-06-07T09:00:00.123456","timeZone":"Europe/Zurich"}
            """;

        var handler = new StubHandler(HttpStatusCode.OK, json);
        var svc = BuildService(handler);

        var result = await svc.GetZurichTimeAsync();

        Assert.Equal(2026, result.Year);
        Assert.Equal(6, result.Month);
        Assert.Equal(7, result.Day);
        Assert.Equal(9, result.Hour);
    }

    [Fact]
    public async Task GetZurichTime_WhenApiFailsTwiceThenSucceeds_ReturnsOnThirdAttempt()
    {
        const string json = """
            {"dateTime":"2026-06-07T10:00:00","timeZone":"Europe/Zurich"}
            """;

        var handler = new SequenceHandler([
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.OK
        ], json);

        var svc = BuildService(handler);
        var result = await svc.GetZurichTimeAsync();

        Assert.Equal(10, result.Hour);
        Assert.Equal(3, handler.CallCount); // succeeded on attempt 3 (max retries = 3)
    }

    [Fact]
    public async Task GetZurichTime_WhenApiAlwaysFails_ThrowsTimeApiUnavailableException()
    {
        var handler = new StubHandler(HttpStatusCode.ServiceUnavailable, "error");
        var svc = BuildService(handler);

        await Assert.ThrowsAsync<TimeApiUnavailableException>(() => svc.GetZurichTimeAsync());
    }

    [Fact]
    public async Task GetZurichTime_WhenApiTimesOut_ThrowsTimeApiUnavailableException()
    {
        var handler = new TimeoutHandler();
        var svc = BuildService(handler);

        await Assert.ThrowsAsync<TimeApiUnavailableException>(() => svc.GetZurichTimeAsync());
    }
}

// ---------------------------------------------------------------------------
// HTTP handler stubs
// ---------------------------------------------------------------------------

file sealed class StubHandler(HttpStatusCode code, string body) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage _, CancellationToken __)
    {
        var response = new HttpResponseMessage(code)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}

file sealed class SequenceHandler(HttpStatusCode[] codes, string successBody) : HttpMessageHandler
{
    public int CallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage _, CancellationToken __)
    {
        var code = CallCount < codes.Length ? codes[CallCount] : codes[^1];
        var body = code == HttpStatusCode.OK ? successBody : "error";
        CallCount++;
        return Task.FromResult(new HttpResponseMessage(code)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        });
    }
}

file sealed class TimeoutHandler : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage _, CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), ct); // Exceeds HttpClient.Timeout = 5s
        return new HttpResponseMessage(HttpStatusCode.OK);
    }
}
