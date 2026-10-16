using Hr.Api.Dtos;
using Hr.Api.Http;
using Hr.Core.Domain;
using Hr.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hr.Api.Controllers;

[ApiController]
[Route("api/correction-drafts")]
public sealed class CorrectionDraftsController(CorrectionDraftService drafts) : ControllerBase
{
    [HttpGet(Name = "CorrectionDrafts_List")]
    [ProducesResponseType<IReadOnlyList<CorrectionDraftDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> List([FromQuery] CorrectionDraftStatus? status, CancellationToken ct) =>
        this.ToActionResult(
            await drafts.ListAsync(status, ct),
            list => list.Select(CorrectionDraftDto.Of).ToList());

    [HttpPost(Name = "CorrectionDrafts_Create")]
    [ProducesResponseType<CorrectionDraftDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Create(CreateCorrectionDraftRequest request, CancellationToken ct) =>
        RejectZonedTime(request.Start, request.End)
        ?? this.ToActionResult(
            await drafts.CreateAsync(request.WorkEntryId, request.Start, request.End, TimeSpan.FromMinutes(request.BreakMinutes), request.Reason, null, ct),
            CorrectionDraftDto.Of,
            StatusCodes.Status201Created);

    [HttpPost("{id:guid}/decision", Name = "CorrectionDrafts_Decide")]
    [ProducesResponseType<CorrectionDraftDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Decide(Guid id, DraftDecisionRequest request, CancellationToken ct) =>
        this.ToActionResult(await drafts.DecideAsync(id, request.Approve, request.Note, ct), CorrectionDraftDto.Of);

    /// <summary>Czas wpisu to czas lokalny bez strefy (ADR-17); wartość ze strefą byłaby po cichu przesunięta, więc jest odrzucana.</summary>
    private IActionResult? RejectZonedTime(params DateTime[] values) =>
        values.Any(v => v.Kind != DateTimeKind.Unspecified)
            ? this.ToActionResult(
                ServiceResult<CorrectionDraft>.Fail(
                    ServiceErrorCode.Validation, "Czas wpisu podaj jako czas lokalny bez strefy, np. 2026-10-12T08:00:00."),
                CorrectionDraftDto.Of)
            : null;
}
