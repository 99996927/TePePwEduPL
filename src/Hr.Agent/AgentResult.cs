using Hr.Core.AgentTools;

namespace Hr.Agent;

public enum AgentFailure
{
    NotFound,
    EmptyQuestion,
    TooLong,
    Unavailable,
    Conflict,
}

/// <param name="UngroundedNumbers">Liczby z odpowiedzi, których nie ma w wynikach narzędzi (ostrzeżenie dla UI i audytu).</param>
public sealed record AgentReply(Guid ConversationId, string Text, IReadOnlyList<StoredMessage> NewMessages, IReadOnlyList<string> UngroundedNumbers);

public sealed record AgentResult(AgentReply? Reply, AgentFailure? Failure)
{
    public static AgentResult Ok(AgentReply reply) => new(reply, null);

    public static AgentResult Fail(AgentFailure failure) => new(null, failure);
}
