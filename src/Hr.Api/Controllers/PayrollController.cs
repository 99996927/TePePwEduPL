using Hr.Api.Dtos;
using Hr.Api.Http;
using Hr.Core.Abstractions;
using Hr.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hr.Api.Controllers;

[ApiController]
[Route("api/payroll")]
public sealed class PayrollController(PayrollService payroll, ICurrentUser user) : ControllerBase
{
    /// <summary>Uruchamia (lub przelicza ponownie) naliczenie za miesiąc dla wszystkich pracowników; tylko kadry.</summary>
    [HttpPost("runs")]
    public async Task<IActionResult> Run(RunPayrollRequest request, CancellationToken ct) =>
        this.ToActionResult(
            await payroll.RunMonthAsync(request.Year, request.Month, ct),
            rows => rows.Select(PayrollResultDto.Of).ToList());

    [HttpGet("results")]
    public async Task<IActionResult> Get([FromQuery] Guid? employeeId, [FromQuery] int year, [FromQuery] int month, CancellationToken ct) =>
        this.ToActionResult(
            await payroll.GetAsync(employeeId ?? user.EmployeeId ?? Guid.Empty, year, month, ct),
            PayrollResultDto.Of);
}
