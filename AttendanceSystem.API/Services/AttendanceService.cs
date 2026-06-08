using AttendanceSystem.API.Data;
using AttendanceSystem.API.DTOs;
using AttendanceSystem.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace AttendanceSystem.API.Services;

public interface IAttendanceService
{
    Task<ClockEventResponse> ClockInAsync(int employeeId, string ipAddress);
    Task<ClockEventResponse> ClockOutAsync(int employeeId, string ipAddress);
    Task<ClockStatusResponse> GetStatusAsync(int employeeId);
    Task<List<ClockEventResponse>> GetHistoryAsync(int employeeId, int? month = null, int? year = null);
    Task<List<ReportEntry>> GetReportsAsync(int? month, int? year, bool includeInactive = false);
    Task<ClockEventResponse?> UpdateClockEventAsync(int eventId, UpdateClockEventRequest request, int adminId);
}

public class AttendanceService : IAttendanceService
{
    private readonly AppDbContext    _db;
    private readonly ITimeService    _timeService;
    private readonly IAuditService   _audit;
    private readonly AppMetrics      _metrics;
    private readonly ILogger<AttendanceService> _logger;

    public AttendanceService(
        AppDbContext db, ITimeService timeService, IAuditService audit,
        AppMetrics metrics, ILogger<AttendanceService> logger)
    {
        _db          = db;
        _timeService = timeService;
        _audit       = audit;
        _metrics     = metrics;
        _logger      = logger;
    }

    public async Task<ClockEventResponse> ClockInAsync(int employeeId, string ipAddress)
    {
        _metrics.ClockInAttempts.Add(1);
        _logger.LogInformation("[CLOCK] ClockIn attempt — EmployeeId={EmployeeId} IP={IP}", employeeId, ipAddress);

        var now = await _timeService.GetZurichTimeAsync();

        // SERIALIZABLE prevents phantom reads in concurrent clock-in race conditions
        await using var tx = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : (IDbContextTransaction?)null;

        var employee = await _db.Employees.Include(e => e.Role).FirstOrDefaultAsync(e => e.EmployeeId == employeeId)
            ?? throw new InvalidOperationException("Employee not found.");

        // Edge case: employee terminated/deactivated after token was issued (token valid up to 15 min)
        if (employee.Status != "Active")
        {
            _logger.LogWarning("[CLOCK] ClockIn blocked — account is {Status} (EmployeeId={EmployeeId})", employee.Status, employeeId);
            throw new AccountInactiveException($"Your account is {employee.Status.ToLower()}. Contact an administrator.");
        }

        var todayStart    = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
        var tomorrowStart = todayStart.AddDays(1);

        var lastEventToday = await _db.ClockEvents
            .Where(ce => ce.EmployeeId == employeeId &&
                         ce.Timestamp  >= todayStart  &&
                         ce.Timestamp  <  tomorrowStart)
            .OrderByDescending(ce => ce.Timestamp)
            .ThenByDescending(ce => ce.EventId)
            .FirstOrDefaultAsync();

        if (lastEventToday?.EventType == "ClockIn")
        {
            _logger.LogWarning("[CLOCK] ClockIn rejected — currently clocked in (EmployeeId={EmployeeId})", employeeId);
            throw new InvalidOperationException("Already clocked in. Clock out first.");
        }

        var clockEvent = new ClockEvent
        {
            EmployeeId = employeeId,
            EventType  = "ClockIn",
            Timestamp  = now,
            CreatedAt  = now
        };

        _db.ClockEvents.Add(clockEvent);
        await _audit.LogAsync(_db, "ClockIn", employeeId, new { ip = ipAddress, timestamp = now }, now);
        await _db.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();

        _metrics.ClockInSuccesses.Add(1);
        _logger.LogInformation("[CLOCK] ClockIn recorded — EmployeeId={EmployeeId} Name={Name} Timestamp={Timestamp} IP={IP}",
            employeeId, employee.FullName, now, ipAddress);

        return new ClockEventResponse(clockEvent.EventId, employeeId, employee.FullName, "ClockIn", now);
    }

    public async Task<ClockEventResponse> ClockOutAsync(int employeeId, string ipAddress)
    {
        _metrics.ClockOutAttempts.Add(1);
        _logger.LogInformation("[CLOCK] ClockOut attempt — EmployeeId={EmployeeId} IP={IP}", employeeId, ipAddress);

        var now = await _timeService.GetZurichTimeAsync();

        // Serializable for relational DBs; InMemory doesn't support transactions
        await using var tx = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : (IDbContextTransaction?)null;

        var employee = await _db.Employees.Include(e => e.Role).FirstOrDefaultAsync(e => e.EmployeeId == employeeId)
            ?? throw new InvalidOperationException("Employee not found.");

        if (employee.Status != "Active")
        {
            _logger.LogWarning("[CLOCK] ClockOut blocked — account is {Status} (EmployeeId={EmployeeId})", employee.Status, employeeId);
            throw new AccountInactiveException($"Your account is {employee.Status.ToLower()}. Contact an administrator.");
        }

        var todayStart    = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
        var tomorrowStart = todayStart.AddDays(1);

        var lastEventToday = await _db.ClockEvents
            .Where(ce => ce.EmployeeId == employeeId &&
                         ce.Timestamp  >= todayStart  &&
                         ce.Timestamp  <  tomorrowStart)
            .OrderByDescending(ce => ce.Timestamp)
            .ThenByDescending(ce => ce.EventId)
            .FirstOrDefaultAsync();

        if (lastEventToday == null || lastEventToday.EventType != "ClockIn")
        {
            _logger.LogWarning("[CLOCK] ClockOut rejected — not currently clocked in (EmployeeId={EmployeeId})", employeeId);
            throw new InvalidOperationException("Cannot clock out: you are not currently clocked in.");
        }

        var clockEvent = new ClockEvent
        {
            EmployeeId = employeeId,
            EventType  = "ClockOut",
            Timestamp  = now,
            CreatedAt  = now
        };

        _db.ClockEvents.Add(clockEvent);
        await _audit.LogAsync(_db, "ClockOut", employeeId, new { ip = ipAddress, timestamp = now }, now);
        await _db.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();

        _metrics.ClockOutSuccesses.Add(1);
        _logger.LogInformation("[CLOCK] ClockOut recorded — EmployeeId={EmployeeId} Name={Name} Timestamp={Timestamp} IP={IP}",
            employeeId, employee.FullName, now, ipAddress);

        return new ClockEventResponse(clockEvent.EventId, employeeId, employee.FullName, "ClockOut", now);
    }

    public async Task<ClockStatusResponse> GetStatusAsync(int employeeId)
    {
        var last = await _db.ClockEvents
            .Where(ce => ce.EmployeeId == employeeId)
            .OrderByDescending(ce => ce.Timestamp)
            .ThenByDescending(ce => ce.EventId)
            .FirstOrDefaultAsync();

        if (last == null)
            return new ClockStatusResponse(false, null);

        return new ClockStatusResponse(
            last.EventType == "ClockIn",
            new ClockStatusLastEvent(last.EventId, last.EventType, last.Timestamp));
    }

    public async Task<List<ClockEventResponse>> GetHistoryAsync(int employeeId, int? month = null, int? year = null)
    {
        var employee = await _db.Employees.FindAsync(employeeId);
        if (employee == null) return [];

        return await _db.ClockEvents
            .Where(ce => ce.EmployeeId == employeeId &&
                (!month.HasValue || ce.Timestamp.Month == month) &&
                (!year.HasValue  || ce.Timestamp.Year  == year))
            .OrderByDescending(ce => ce.Timestamp)
            .Select(ce => new ClockEventResponse(
                ce.EventId, ce.EmployeeId,
                employee.FirstName + " " + employee.LastName,
                ce.EventType, ce.Timestamp))
            .ToListAsync();
    }

    public async Task<List<ReportEntry>> GetReportsAsync(int? month, int? year, bool includeInactive = false)
    {
        var query = _db.Employees.Include(e => e.ClockEvents).AsQueryable();

        if (!includeInactive)
            query = query.Where(e => e.Status == "Active");

        var employees = await query
            .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
            .ToListAsync();

        return employees.Select(emp =>
        {
            var events = emp.ClockEvents
                .Where(ce =>
                    (!month.HasValue || ce.Timestamp.Month == month) &&
                    (!year.HasValue  || ce.Timestamp.Year  == year))
                .OrderBy(ce => ce.Timestamp)
                .ToList();

            var daily = events
                .GroupBy(ce => DateOnly.FromDateTime(ce.Timestamp.DateTime))
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var dayEvents = g.OrderBy(ce => ce.Timestamp).ThenBy(ce => ce.EventId).ToList();
                    var sessions = new List<SessionEntry>();
                    DateTimeOffset? openClockIn = null;
                    foreach (var ev in dayEvents)
                    {
                        if (ev.EventType == "ClockIn")
                        {
                            openClockIn = ev.Timestamp;
                        }
                        else if (ev.EventType == "ClockOut" && openClockIn.HasValue)
                        {
                            var mins = (int)(ev.Timestamp - openClockIn.Value).TotalMinutes;
                            sessions.Add(new SessionEntry(openClockIn.Value, ev.Timestamp, mins, true));
                            openClockIn = null;
                        }
                    }
                    if (openClockIn.HasValue)
                        sessions.Add(new SessionEntry(openClockIn.Value, null, 0, false));

                    int totalMins = sessions.Sum(s => s.MinutesWorked);
                    bool isComplete = sessions.Count > 0 && sessions.All(s => s.IsComplete);
                    return new DailyEntry(g.Key, sessions, totalMins, isComplete);
                })
                .ToList();

            return new ReportEntry(
                emp.EmployeeId, emp.FullName, emp.Email,
                daily.Sum(d => d.TotalMinutesWorked),
                events.Count, daily);
        }).ToList();
    }

    public async Task<ClockEventResponse?> UpdateClockEventAsync(int eventId, UpdateClockEventRequest request, int adminId)
    {
        var clockEvent = await _db.ClockEvents
            .Include(ce => ce.Employee)
            .FirstOrDefaultAsync(ce => ce.EventId == eventId);

        if (clockEvent == null) return null;

        var now = await _timeService.GetZurichTimeAsync();

        // Serializable for relational DBs; InMemory doesn't support transactions
        await using var tx = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : (IDbContextTransaction?)null;

        var oldTimestamp = clockEvent.Timestamp;
        clockEvent.Timestamp = request.Timestamp;

        _logger.LogInformation("[ADMIN] ClockEvent updated — EventId={EventId} AdminId={AdminId} Old={Old} New={New}",
            eventId, adminId, oldTimestamp, request.Timestamp);

        await _audit.LogAsync(_db, "AdminAction", adminId,
            new { eventId, oldTimestamp, newTimestamp = request.Timestamp }, now);
        await _db.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();

        return new ClockEventResponse(
            clockEvent.EventId, clockEvent.EmployeeId,
            clockEvent.Employee.FullName,
            clockEvent.EventType, clockEvent.Timestamp);
    }
}

public class AccountInactiveException(string message) : Exception(message);
