using Hr.Agent;
using Hr.Api.Dtos;
using Hr.Core.AgentTools;
using Microsoft.AspNetCore.Mvc;

namespace Hr.Api.Controllers;

[ApiController]
[Route("api/conversations")]
public sealed class ConversationsController(AgentRunner agent, IConversationStore store) : ControllerBase
{
    [HttpPost("messages", Name = "Conversations_Ask")]
    [ProducesResponseType<AskResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> Ask(AskRequest request, CancellationToken ct)
    {
        var result = await agent.AskAsync(request.ConversationId, request.Text, ct);
        if (result.Reply is { } reply)
        {
            return Ok(new AskResponse(reply.ConversationId, reply.Text, reply.NewMessages.Select(MessageDto.Of).ToList(), reply.UngroundedNumbers));
        }

        return result.Failure switch
        {
            AgentFailure.NotFound => Failure(StatusCodes.Status404NotFound, "NotFound", "Nie znaleziono zasobu.", "Nie znaleziono rozmowy.", null),
            AgentFailure.EmptyQuestion => Failure(StatusCodes.Status422UnprocessableEntity, "Validation", "Naruszenie reguł.", "Pytanie nie może być puste.", null),
            AgentFailure.TooLong => Failure(
                StatusCodes.Status422UnprocessableEntity, "Validation", "Naruszenie reguł.", $"Pytanie może mieć najwyżej {AgentRunner.MaxQuestionLength} znaków.", null),
            AgentFailure.Conflict => Failure(StatusCodes.Status409Conflict, "Conflict", "Konflikt stanu zasobu.", "Rozmowa zmieniła się w międzyczasie; spróbuj ponownie.", result.ConversationId),
            _ => Failure(
                StatusCodes.Status503ServiceUnavailable, "AgentUnavailable", "Agent jest chwilowo niedostępny.",
                "Nie udało się połączyć z modelem. Pytanie zostało zapisane w rozmowie; reszta systemu działa.", result.ConversationId),
        };
    }

    [HttpGet(Name = "Conversations_List")]
    public async Task<ActionResult<IReadOnlyList<ConversationSummaryDto>>> List(CancellationToken ct) =>
        Ok((await store.ListAsync(ct)).Select(ConversationSummaryDto.Of).ToList());

    [HttpGet("{id:guid}", Name = "Conversations_Get")]
    [ProducesResponseType<ConversationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var opened = await store.OpenAsync(id, ct);
        return opened.IsSuccess
            ? Ok(ConversationDetailDto.Of(opened.Value!))
            : Failure(StatusCodes.Status404NotFound, "NotFound", "Nie znaleziono zasobu.", "Nie znaleziono rozmowy.", null);
    }

    private ObjectResult Failure(int status, string code, string title, string detail, Guid? conversationId)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problem.Extensions["code"] = code;
        if (conversationId is not null)
        {
            problem.Extensions["conversationId"] = conversationId;
        }

        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
