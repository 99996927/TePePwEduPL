using Hr.Api.Dtos;
using Hr.Api.Http;
using Hr.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hr.Api.Controllers;

[ApiController]
[Route("api/audit")]
public sealed class AuditController(AuditService audit) : ControllerBase
{
    /// <summary>Dziennik audytu od najnowszych wpisów; tylko kadry. Czasy <c>from</c> i <c>to</c> w UTC (np. <c>2026-10-16T09:00:00Z</c>).</summary>
    [HttpGet(Name = "Audit_Query")]
    [ProducesResponseType<AuditPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Query(
        [FromQuery] string? entityType, [FromQuery] string? entityId, [FromQuery] Guid? actorUserId,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default) =>
        this.ToActionResult(
            await audit.QueryAsync(new AuditQuery(entityType, entityId, actorUserId, from?.UtcDateTime, to?.UtcDateTime, skip, take), ct),
            AuditPageDto.Of);
}
