using Hr.Api.Dtos;
using Hr.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hr.Api.Controllers;

[ApiController]
[Route("api/employees")]
public sealed class EmployeesController(EmployeeDirectoryService directory) : ControllerBase
{
    /// <summary>Pracownicy widoczni dla zalogowanego użytkownika (zakres wg roli), bez wynagrodzeń.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmployeeDto>>> List(CancellationToken ct) =>
        Ok((await directory.ListAsync(ct)).Select(EmployeeDto.Of).ToList());
}
