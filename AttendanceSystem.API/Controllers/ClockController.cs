using AttendanceSystem.API.DTOs;
using AttendanceSystem.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AttendanceSystem.API.Controllers;

/// <summary>Manages clock-in/out events and attendance history for the authenticated employee.</summary>
[ApiController]
[Route("api/clock")]
[Authorize]
[Produces("application/json")]
public class ClockController(IAttendanceService attendanceService) : ControllerBase
{
    /// <summary>
    /// Records a clock-in event for the authenticated employee.
    /// </summary>
    /// <remarks>
    /// The timestamp is captured from the configured time service (TimeAPI) to prevent
    /// client-side manipulation. The caller's IP address is logged alongside the event.
    /// </remarks>
    /// <response code="200">Clock-in recorded successfully. Returns the new clock event.</response>
    /// <response code="401">Not authenticated — valid <c>access_token</c> cookie required.</response>
    /// <response code="409">Employee is already clocked in; clock out first.</response>
    [HttpPost("in")]
    [ProducesResponseType(typeof(ClockEventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClockEventResponse>> ClockIn()
    {
        var employeeId = GetEmployeeId();
        var ip = GetClientIp();
        var result = await attendanceService.ClockInAsync(employeeId, ip);
        return Ok(result);
    }

    /// <summary>
    /// Records a clock-out event for the authenticated employee.
    /// </summary>
    /// <remarks>
    /// The timestamp is captured from the configured time service (TimeAPI) to prevent
    /// client-side manipulation. The caller's IP address is logged alongside the event.
    /// </remarks>
    /// <response code="200">Clock-out recorded successfully. Returns the new clock event.</response>
    /// <response code="401">Not authenticated — valid <c>access_token</c> cookie required.</response>
    /// <response code="409">Employee is not currently clocked in; clock in first.</response>
    [HttpPost("out")]
    [ProducesResponseType(typeof(ClockEventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClockEventResponse>> ClockOut()
    {
        var employeeId = GetEmployeeId();
        var ip = GetClientIp();
        var result = await attendanceService.ClockOutAsync(employeeId, ip);
        return Ok(result);
    }

    /// <summary>
    /// Returns the current clock-in status for the authenticated employee.
    /// </summary>
    /// <response code="200">
    /// Returns <c>isClockedIn</c> (bool) and details of the last clock event, if any.
    /// <c>lastEvent</c> is <c>null</c> when the employee has no recorded events.
    /// </response>
    /// <response code="401">Not authenticated — valid <c>access_token</c> cookie required.</response>
    [HttpGet("status")]
    [ProducesResponseType(typeof(ClockStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ClockStatusResponse>> GetStatus()
    {
        var employeeId = GetEmployeeId();
        return Ok(await attendanceService.GetStatusAsync(employeeId));
    }

    /// <summary>
    /// Returns the clock event history for the authenticated employee.
    /// </summary>
    /// <remarks>
    /// Events are returned in ascending timestamp order. Omitting both <paramref name="month"/>
    /// and <paramref name="year"/> returns the complete history.
    /// </remarks>
    /// <param name="month">Calendar month to filter by (1–12). Omit to include all months.</param>
    /// <param name="year">Four-digit year to filter by. Omit to include all years.</param>
    /// <response code="200">Returns the list of clock-in and clock-out events matching the filter.</response>
    /// <response code="401">Not authenticated — valid <c>access_token</c> cookie required.</response>
    [HttpGet("history")]
    [ProducesResponseType(typeof(List<ClockEventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<ClockEventResponse>>> GetHistory([FromQuery] int? month, [FromQuery] int? year)
    {
        var employeeId = GetEmployeeId();
        return Ok(await attendanceService.GetHistoryAsync(employeeId, month, year));
    }

    private int GetEmployeeId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new InvalidOperationException("No employee ID in token");
        return int.Parse(sub);
    }

    private string GetClientIp() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
