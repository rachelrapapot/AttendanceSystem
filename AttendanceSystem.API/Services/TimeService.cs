using Polly;
using Polly.CircuitBreaker;
using System.Diagnostics;
using System.Text.Json;

namespace AttendanceSystem.API.Services;

public interface ITimeService
{
    Task<DateTimeOffset> GetZurichTimeAsync();
}

public class TimeService : ITimeService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TimeService> _logger;
    private readonly AppMetrics _metrics;
    private readonly ResiliencePipeline<DateTimeOffset> _pipeline;

    private const string TimeApiUrl = "https://timeapi.io/api/time/current/zone?timeZone=Europe/Zurich";

    public TimeService(HttpClient httpClient, ILogger<TimeService> logger, AppMetrics metrics)
    {
        _httpClient = httpClient;
        _logger     = logger;
        _metrics    = metrics;

        _pipeline = new ResiliencePipelineBuilder<DateTimeOffset>()
            .AddRetry(new Polly.Retry.RetryStrategyOptions<DateTimeOffset>
            {
                MaxRetryAttempts = 3,
                Delay            = TimeSpan.FromMilliseconds(500),
                BackoffType      = DelayBackoffType.Exponential,
                OnRetry = args =>
                {
                    _logger.LogWarning("[TIMEAPI] Retry {Attempt}/3 after {Delay:F0}ms delay",
                        args.AttemptNumber + 1, args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions<DateTimeOffset>
            {
                FailureRatio      = 0.5,
                MinimumThroughput = 3,
                SamplingDuration  = TimeSpan.FromSeconds(30),
                BreakDuration     = TimeSpan.FromSeconds(30),
                OnOpened = args =>
                {
                    _logger.LogError("[TIMEAPI] Circuit breaker OPENED — service unavailable for 30s");
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("[TIMEAPI] Circuit breaker closed — service restored");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task<DateTimeOffset> GetZurichTimeAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await _pipeline.ExecuteAsync(async ct =>
            {
                _logger.LogDebug("[TIMEAPI] Calling {Url}", TimeApiUrl);
                var response = await _httpClient.GetAsync(TimeApiUrl, ct);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);

                var dateTime = doc.RootElement.GetProperty("dateTime").GetString()
                    ?? throw new InvalidOperationException("Missing dateTime field in TimeAPI response");

                return DateTimeOffset.Parse(dateTime);
            });

            sw.Stop();
            _metrics.TimeApiLatencyMs.Record(sw.Elapsed.TotalMilliseconds);
            _logger.LogDebug("[TIMEAPI] Success in {Ms:F0}ms → {Time}", sw.Elapsed.TotalMilliseconds, result);
            return result;
        }
        catch (BrokenCircuitException ex)
        {
            sw.Stop();
            _metrics.TimeApiFailures.Add(1);
            _logger.LogError(ex, "[TIMEAPI] Circuit open — refusing call after {Ms:F0}ms", sw.Elapsed.TotalMilliseconds);
            throw new TimeApiUnavailableException("Time service is temporarily unavailable. Please try again later.", ex);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _metrics.TimeApiFailures.Add(1);
            _logger.LogError(ex, "[TIMEAPI] Failed after {Ms:F0}ms and all 3 retries", sw.Elapsed.TotalMilliseconds);
            throw new TimeApiUnavailableException("Time service is unreachable. Please try again later.", ex);
        }
    }
}

public class TimeApiUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);
