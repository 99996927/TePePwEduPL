namespace Hr.Core.Domain;

public class Conversation
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public List<Message> Messages { get; set; } = new();
}
