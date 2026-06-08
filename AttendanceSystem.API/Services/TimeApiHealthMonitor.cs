namespace AttendanceSystem.API.Services;

public sealed class TimeApiHealthMonitor(
    IServiceScopeFactory scopeFactory,
    ILogger<TimeApiHealthMonitor> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval  = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan AlertThreshold = TimeSpan.FromMinutes(5);

    private DateTimeOffset? _firstFailureAt;
    private bool            _criticalFired;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Give the app time to finish startup before the first probe
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            await CheckAsync();
            try { await Task.Delay(CheckInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task CheckAsync()
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var ts = scope.ServiceProvider.GetRequiredService<ITimeService>();
            await ts.GetZurichTimeAsync();

            if (_firstFailureAt.HasValue)
            {
                var recovered = DateTimeOffset.UtcNow - _firstFailureAt.Value;
                logger.LogInformation("[HEALTH] TimeAPI recovered after {Minutes:F1} min downtime", recovered.TotalMinutes);
            }

            _firstFailureAt = null;
            _criticalFired  = false;
        }
        catch (Exception ex)
        {
            _firstFailureAt ??= DateTimeOffset.UtcNow;
            var elapsed = DateTimeOffset.UtcNow - _firstFailureAt.Value;

            logger.LogWarning("[HEALTH] TimeAPI unavailable — {Elapsed:F0}s elapsed: {Error}",
                elapsed.TotalSeconds, ex.Message);

            if (elapsed >= AlertThreshold && !_criticalFired)
            {
                logger.LogCritical(
                    "[ALERT] TimeAPI has been unreachable for {Minutes:F0} min — " +
                    "all clock-in/out operations are returning 503",
                    elapsed.TotalMinutes);
                _criticalFired = true;
            }
        }
    }
}
