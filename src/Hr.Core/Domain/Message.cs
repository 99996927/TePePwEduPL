namespace Hr.Core.Domain;

public enum MessageRole
{
    User,
    Assistant,
    ToolCall,
    ToolResult,
}

public class Message
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public int Seq { get; set; }

    public MessageRole Role { get; set; }

    public string Content { get; set; } = string.Empty;

    public string? ToolName { get; set; }

    public string? ToolParamsJson { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
