using System.Diagnostics.Metrics;

namespace AttendanceSystem.API.Services;

// Observable via: dotnet-counters monitor --name AttendanceSystem.API
// Or wire an OpenTelemetry exporter (Prometheus, Application Insights, etc.)
public sealed class AppMetrics : IDisposable
{
    private readonly Meter _meter = new("AttendanceSystem", "1.0.0");

    // Clock events
    public Counter<long> ClockInAttempts  { get; }
    public Counter<long> ClockInSuccesses { get; }
    public Counter<long> ClockOutAttempts  { get; }
    public Counter<long> ClockOutSuccesses { get; }

    // Auth
    public Counter<long> LoginFailures { get; }

    // TimeAPI
    public Counter<long>         TimeApiFailures  { get; }
    public Histogram<double>     TimeApiLatencyMs { get; }

    // HTTP
    public Histogram<double> RequestDurationMs { get; }

    public AppMetrics()
    {
        ClockInAttempts   = _meter.CreateCounter<long>("attendance.clockin.attempts",   description: "Total clock-in attempts");
        ClockInSuccesses  = _meter.CreateCounter<long>("attendance.clockin.successes",  description: "Successful clock-in events");
        ClockOutAttempts  = _meter.CreateCounter<long>("attendance.clockout.attempts",  description: "Total clock-out attempts");
        ClockOutSuccesses = _meter.CreateCounter<long>("attendance.clockout.successes", description: "Successful clock-out events");
        LoginFailures     = _meter.CreateCounter<long>("auth.login.failures",           description: "Failed login attempts");
        TimeApiFailures   = _meter.CreateCounter<long>("timeapi.failures",              description: "TimeAPI call failures after all retries");
        TimeApiLatencyMs  = _meter.CreateHistogram<double>("timeapi.latency_ms",        unit: "ms", description: "TimeAPI successful call latency");
        RequestDurationMs = _meter.CreateHistogram<double>("http.server.request.duration_ms", unit: "ms", description: "HTTP request duration");
    }

    public void Dispose() => _meter.Dispose();
}
