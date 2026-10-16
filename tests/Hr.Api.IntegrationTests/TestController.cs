using Hr.Api.Http;
using Hr.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hr.Api.IntegrationTests;

/// <summary>Trasy pomocnicze tylko dla testów: chroniony endpoint, mapowanie błędów serwisów i wyjątek.</summary>
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("test")]
public sealed class TestController : ControllerBase
{
    [HttpGet("secure")]
    public IActionResult Secure() => Ok();

    [AllowAnonymous]
    [HttpGet("result/{kind}")]
    public IActionResult Result(string kind) =>
        this.ToActionResult(ServiceResult<int>.Fail(Enum.Parse<ServiceErrorCode>(kind, ignoreCase: true), "Komunikat testowy", "KP-154-1"), x => x);

    [AllowAnonymous]
    [HttpGet("boom")]
    public IActionResult Boom() => throw new InvalidOperationException("Sekret wewnętrzny");
}
