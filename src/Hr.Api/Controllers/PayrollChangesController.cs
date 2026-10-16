using Hr.Api.Dtos;
using Hr.Api.Http;
using Hr.Core.Domain;
using Hr.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hr.Api.Controllers;

[ApiController]
[Route("api/payroll-changes")]
public sealed class PayrollChangesController(PayrollChangeService changes) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PayrollChangeDto>>> List([FromQuery] Guid? employeeId, CancellationToken ct) =>
        Ok((await changes.ListAsync(employeeId, ct)).Select(PayrollChangeDto.Of).ToList());

    [HttpPost]
    public async Task<IActionResult> Create(CreatePayrollChangeRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<PayrollComponent>(request.Component, ignoreCase: true, out var component)
            || !Enum.IsDefined(component) || int.TryParse(request.Component, out _))
        {
            return this.ToActionResult(
                ServiceResult<PayrollChange>.Fail(ServiceErrorCode.Validation, "Nieznany składnik; dozwolone: BaseSalary, FixedAllowance."),
                PayrollChangeDto.Of);
        }

        return this.ToActionResult(
            await changes.CreateAsync(request.EmployeeId, component, request.Value, request.EffectiveFrom, request.Reason, ct),
            PayrollChangeDto.Of,
            StatusCodes.Status201Created);
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct) =>
        this.ToActionResult(await changes.SubmitAsync(id, ct), PayrollChangeDto.Of);

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct) =>
        this.ToActionResult(await changes.ApproveAsync(id, ct), PayrollChangeDto.Of);

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, RejectRequest request, CancellationToken ct) =>
        this.ToActionResult(await changes.RejectAsync(id, request.Reason, ct), PayrollChangeDto.Of);
}
