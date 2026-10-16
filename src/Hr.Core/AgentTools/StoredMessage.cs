namespace Hr.Core.AgentTools;

/// <summary>Rola wiadomości w rozmowie; odpowiada <c>Hr.Core.Domain.MessageRole</c> (te same nazwy), ale agent nie widzi typów domeny.</summary>
public enum StoredRole
{
    User,
    Assistant,
    ToolCall,
    ToolResult,
}

public sealed record StoredMessage(Guid Id, int Seq, StoredRole Role, string Content, string? ToolName, string? ToolParamsJson, DateTime CreatedAtUtc);

public sealed record ConversationSummary(Guid Id, Guid UserId, DateTime StartedAtUtc, string? Preview, int MessageCount);

public sealed record OpenedConversation(Guid Id, Guid UserId, DateTime StartedAtUtc, IReadOnlyList<StoredMessage> Messages);
