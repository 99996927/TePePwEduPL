using Hr.Api.Dtos;
using Hr.Api.Http;
using Hr.Core.Abstractions;
using Hr.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hr.Api.Controllers;

[ApiController]
[Route("api/timesheet")]
public sealed class TimesheetController(TimesheetService timesheet, ICurrentUser user) : ControllerBase
{
    [HttpGet("entries", Name = "Timesheet_List")]
    public async Task<ActionResult<IReadOnlyList<WorkEntryDto>>> List([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        Ok((await timesheet.ListAsync(from, to, ct)).Select(WorkEntryDto.Of).ToList());

    [HttpPost("entries", Name = "Timesheet_Add")]
    [ProducesResponseType<WorkEntryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Add(AddEntryRequest request, CancellationToken ct) =>
        this.ToActionResult(
            await timesheet.AddEntryAsync(request.Start, request.End, TimeSpan.FromMinutes(request.BreakMinutes), ct),
            WorkEntryDto.Of,
            StatusCodes.Status201Created);

    [HttpPut("entries/{id:guid}", Name = "Timesheet_Correct")]
    [ProducesResponseType<WorkEntryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Correct(Guid id, CorrectEntryRequest request, CancellationToken ct) =>
        this.ToActionResult(
            await timesheet.CorrectEntryAsync(id, request.Start, request.End, TimeSpan.FromMinutes(request.BreakMinutes), request.Reason, ct),
            WorkEntryDto.Of);

    [HttpGet("analysis", Name = "Timesheet_Analyze")]
    [ProducesResponseType<TimesheetAnalysisDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Analyze([FromQuery] Guid? employeeId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        this.ToActionResult(
            await timesheet.AnalyzeAsync(employeeId ?? user.EmployeeId ?? Guid.Empty, from, to, ct),
            TimesheetAnalysisDto.Of);
}
