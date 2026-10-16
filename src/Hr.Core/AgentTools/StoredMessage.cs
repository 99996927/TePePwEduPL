using Hr.Core.Domain;

namespace Hr.Core.AgentTools;

public sealed record StoredMessage(Guid Id, int Seq, MessageRole Role, string Content, string? ToolName, string? ToolParamsJson, DateTime CreatedAtUtc);

public sealed record ConversationSummary(Guid Id, Guid UserId, DateTime StartedAtUtc, string? Preview, int MessageCount);
