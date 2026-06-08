using AttendanceSystem.API.DTOs;
using AttendanceSystem.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AttendanceSystem.API.Controllers;

/// <summary>
/// Administrative endpoints for managing employees, generating reports, reviewing audit logs,
/// and correcting clock event records. All endpoints require the <c>Admin</c> role.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class AdminController(IAdminService adminService, IAttendanceService attendanceService) : ControllerBase
{
    /// <summary>
    /// Returns all employees, including inactive and terminated accounts.
    /// </summary>
    /// <response code="200">Returns the full list of employees with their current status.</response>
    /// <response code="401">Not authenticated — valid <c>access_token</c> cookie required.</response>
    /// <response code="403">Authenticated user does not have the <c>Admin</c> role.</response>
    [HttpGet("employees")]
    [ProducesResponseType(typeof(List<EmployeeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<EmployeeResponse>>> GetEmployees() =>
        Ok(await adminService.GetEmployeesAsync());

    /// <summary>
    /// Creates a new employee account and sends the initial credentials.
    /// </summary>
    /// <remarks>
    /// The password is hashed with BCrypt before storage and never persisted in plain text.
    /// Password requirements: minimum 8 characters, at least one uppercase letter, one digit,
    /// and one special character.
    /// </remarks>
    /// <param name="request">New employee details: email, password, first name, last name, and role (<c>Employee</c> or <c>Admin</c>).</param>
    /// <response code="201">Employee created successfully. Returns the new employee record.</response>
    /// <response code="400">Validation failed — duplicate email, weak password, invalid role, or missing required fields.</response>
    /// <response code="401">Not authenticated — valid <c>access_token</c> cookie required.</response>
    /// <response code="403">Authenticated user does not have the <c>Admin</c> role.</response>
    [HttpPost("employees")]
    [ProducesResponseType(typeof(EmployeeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EmployeeResponse>> CreateEmployee([FromBody] CreateEmployeeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await adminService.CreateEmployeeAsync(request, GetAdminId());
        return CreatedAtAction(nameof(GetEmployees), new { id = result.EmployeeId }, result);
    }

    /// <summary>
    /// Updates an existing employee's name, password, or account status.
    /// </summary>
    /// <remarks>
    /// All fields are optional; only the fields that are provided will be updated.
    /// Valid status values are <c>Active</c>, <c>Inactive</c>, and <c>Terminated</c>.
    /// Setting status to <c>Terminated</c> via this endpoint does not revoke refresh tokens —
    /// use <c>DELETE /api/admin/employees/{id}</c> for a full termination.
    /// </remarks>
    /// <param name="id">ID of the employee to update.</param>
    /// <param name="request">Fields to update. Omit any field to leave it unchanged.</param>
    /// <response code="200">Employee updated. Returns the updated employee record.</response>
    /// <response code="400">Validation failed — weak password, invalid status value, or other constraint violation.</response>
    /// <response code="401">Not authenticated — valid <c>access_token</c> cookie required.</response>
    /// <response code="403">Authenticated user does not have the <c>Admin</c> role.</response>
    /// <response code="404">No employee with the specified <paramref name="id"/> exists.</response>
    [HttpPut("employees/{id:int}")]
    [ProducesResponseType(typeof(EmployeeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeResponse>> UpdateEmployee(int id, [FromBody] UpdateEmployeeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await adminService.UpdateEmployeeAsync(id, request, GetAdminId());
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Terminates an employee, setting their status to <c>Terminated</c> and revoking all active refresh tokens.
    /// </summary>
    /// <remarks>
    /// Termination is a soft delete: the employee record is retained for audit and reporting purposes.
    /// All stored refresh tokens for the employee are invalidated immediately, forcing any active
    /// sessions to fail on the next token refresh. The employee can no longer log in after termination.
    /// This action is audited.
    /// </remarks>
    /// <param name="id">ID of the employee to terminate.</param>
    /// <response code="204">Employee terminated and sessions revoked.</response>
    /// <response code="401">Not authenticated — valid <c>access_token</c> cookie required.</response>
    /// <response code="403">Authenticated user does not have the <c>Admin</c> role.</response>
    /// <response code="404">No employee with the specified <paramref name="id"/> exists.</response>
    [HttpDelete("employees/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TerminateEmployee(int id)
    {
        var success = await adminService.TerminateEmployeeAsync(id, GetAdminId());
        return success ? NoContent() : NotFound();
    }

    /// <summary>
    /// Generates an attendance report for all employees, with optional filters.
    /// </summary>
    /// <remarks>
    /// Each entry in the response contains the employee's total minutes worked, total clock events,
    /// and a day-by-day breakdown for the filtered period. Partial days (clocked in but not yet
    /// out) are included with <c>isComplete: false</c>.
    /// </remarks>
    /// <param name="month">Calendar month to filter by (1–12). Omit to include all months.</param>
    /// <param name="year">Four-digit year to filter by. Omit to include all years.</param>
    /// <param name="includeInactive">
    /// When <c>true</c>, includes employees with <c>Inactive</c> or <c>Terminated</c> status.
    /// Defaults to <c>false</c> (active employees only).
    /// </param>
    /// <response code="200">Returns aggregated attendance data with daily breakdowns per employee.</response>
    /// <response code="401">Not authenticated — valid <c>access_token</c> cookie required.</response>
    /// <response code="403">Authenticated user does not have the <c>Admin</c> role.</response>
    [HttpGet("reports")]
    [ProducesResponseType(typeof(List<ReportEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<ReportEntry>>> GetReports(
        [FromQuery] int? month,
        [FromQuery] int? year,
        [FromQuery] bool includeInactive = false) =>
        Ok(await attendanceService.GetReportsAsync(month, year, includeInactive));

    /// <summary>
    /// Returns all audit log entries, ordered by timestamp ascending.
    /// </summary>
    /// <remarks>
    /// The audit log records admin actions (employee creation, updates, terminations, clock event
    /// corrections) and authentication events (login attempts, logouts). Each entry includes the
    /// acting employee's ID, the action performed, optional detail text, and timestamps.
    /// </remarks>
    /// <response code="200">Returns the complete system audit trail.</response>
    /// <response code="401">Not authenticated — valid <c>access_token</c> cookie required.</response>
    /// <response code="403">Authenticated user does not have the <c>Admin</c> role.</response>
    [HttpGet("audit-logs")]
    [ProducesResponseType(typeof(List<AuditLogResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<AuditLogResponse>>> GetAuditLogs() =>
        Ok(await adminService.GetAuditLogsAsync());

    /// <summary>
    /// Corrects the timestamp of an existing clock event.
    /// </summary>
    /// <remarks>
    /// Use this to fix clock events recorded with an incorrect time (e.g., due to a system clock
    /// error or a missed clock-in/out). The correction is audited and attributed to the admin
    /// making the request.
    /// </remarks>
    /// <param name="id">ID of the clock event to correct.</param>
    /// <param name="request">The corrected timestamp (must include timezone offset).</param>
    /// <response code="200">Clock event updated. Returns the modified record.</response>
    /// <response code="400">Request body is missing or the timestamp value is invalid.</response>
    /// <response code="401">Not authenticated — valid <c>access_token</c> cookie required.</response>
    /// <response code="403">Authenticated user does not have the <c>Admin</c> role.</response>
    /// <response code="404">No clock event with the specified <paramref name="id"/> exists.</response>
    [HttpPut("clock-events/{id:int}")]
    [ProducesResponseType(typeof(ClockEventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClockEventResponse>> UpdateClockEvent(int id, [FromBody] UpdateClockEventRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await attendanceService.UpdateClockEventAsync(id, request, GetAdminId());
        return result == null ? NotFound() : Ok(result);
    }

    private int GetAdminId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new InvalidOperationException("No employee ID in token");
        return int.Parse(sub);
    }
}
