using Hr.Core.AgentTools;

namespace Hr.Api.Dtos;

public sealed record AskRequest(Guid? ConversationId, string Text);

/// <param name="ToolParams">Parametry wywołania narzędzia jako tekst JSON (tylko dla wiadomości <c>ToolCall</c>).</param>
public sealed record MessageDto(int Seq, string Role, string Content, string? ToolName, string? ToolParams, DateTime CreatedAtUtc)
{
    public static MessageDto Of(StoredMessage m) => new(m.Seq, m.Role.ToString(), m.Content, m.ToolName, m.ToolParamsJson, m.CreatedAtUtc);
}

/// <param name="UngroundedNumbers">Liczby z odpowiedzi, których nie potwierdzają wyniki narzędzi; niepuste oznacza, że odpowiedzi nie należy traktować jako pewnej.</param>
public sealed record AskResponse(Guid ConversationId, string Text, IReadOnlyList<MessageDto> Messages, IReadOnlyList<string> UngroundedNumbers);

public sealed record ConversationSummaryDto(Guid Id, Guid UserId, DateTime StartedAtUtc, string? Preview, int MessageCount)
{
    public static ConversationSummaryDto Of(ConversationSummary s) => new(s.Id, s.UserId, s.StartedAtUtc, s.Preview, s.MessageCount);
}

public sealed record ConversationDetailDto(Guid Id, Guid UserId, DateTime StartedAtUtc, IReadOnlyList<MessageDto> Messages)
{
    public static ConversationDetailDto Of(OpenedConversation c) => new(c.Id, c.UserId, c.StartedAtUtc, c.Messages.Select(MessageDto.Of).ToList());
}
