using Hr.Api.Dtos;
using Hr.Api.Http;
using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hr.Api.Controllers;

[ApiController]
[Route("api/leave")]
public sealed class LeaveController(LeaveService leave, ICurrentUser user) : ControllerBase
{
    [HttpGet("balance", Name = "Leave_Balance")]
    [ProducesResponseType<LeaveBalanceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Balance([FromQuery] Guid? employeeId, CancellationToken ct) =>
        this.ToActionResult(await leave.GetBalanceAsync(employeeId ?? user.EmployeeId ?? Guid.Empty, ct), LeaveBalanceDto.Of);

    [HttpGet("requests", Name = "Leave_List")]
    public async Task<ActionResult<IReadOnlyList<LeaveRequestDto>>> List(CancellationToken ct) =>
        Ok((await leave.ListAsync(ct)).Select(LeaveRequestDto.Of).ToList());

    [HttpGet("requests/{id:guid}", Name = "Leave_Get")]
    [ProducesResponseType<LeaveRequestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var found = (await leave.ListAsync(ct)).FirstOrDefault(l => l.Id == id);
        return this.ToActionResult(
            found is null
                ? ServiceResult<LeaveRequest>.Fail(ServiceErrorCode.NotFound, "Nie znaleziono wniosku.")
                : ServiceResult<LeaveRequest>.Ok(found),
            LeaveRequestDto.Of);
    }

    [HttpPost("requests", Name = "Leave_Submit")]
    [ProducesResponseType<LeaveRequestDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Submit(SubmitLeaveRequest request, CancellationToken ct)
    {
        var result = await leave.SubmitAsync(request.From, request.To, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, LeaveRequestDto.Of(result.Value))
            : this.ToActionResult(result, LeaveRequestDto.Of);
    }

    [HttpPost("requests/{id:guid}/cancel", Name = "Leave_Cancel")]
    [ProducesResponseType<LeaveRequestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) =>
        this.ToActionResult(await leave.CancelAsync(id, ct), LeaveRequestDto.Of);

    [HttpPost("requests/{id:guid}/decision", Name = "Leave_Decide")]
    [ProducesResponseType<LeaveRequestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Decide(Guid id, LeaveDecisionRequest request, CancellationToken ct) =>
        this.ToActionResult(await leave.DecideAsync(id, request.Approve, request.Reason, ct), LeaveRequestDto.Of);
}
