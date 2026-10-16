using Hr.Core.AgentTools;

namespace Hr.Agent.Tests;

/// <summary>Magazyn rozmów w pamięci jednego użytkownika; identyfikatory z <see cref="Foreign"/> należą do kogoś innego (NotFound).</summary>
public sealed class FakeConversationStore : IConversationStore
{
    private readonly Dictionary<Guid, List<StoredMessage>> _conversations = new();

    public HashSet<Guid> Foreign { get; } = new();

    public List<(Guid ConversationId, string EventType, string Details)> Warnings { get; } = new();

    public IReadOnlyList<StoredMessage> Messages(Guid conversationId) => _conversations[conversationId];

    public IEnumerable<Guid> ConversationIds => _conversations.Keys;

    public Task<StoreResult<Guid>> StartAsync(CancellationToken ct)
    {
        var id = Guid.NewGuid();
        _conversations[id] = new List<StoredMessage>();
        return Task.FromResult(StoreResult<Guid>.Ok(id));
    }

    public Task<StoreResult<IReadOnlyList<StoredMessage>>> LoadAsync(Guid conversationId, int lastN, CancellationToken ct) =>
        Task.FromResult(_conversations.TryGetValue(conversationId, out var list) && !Foreign.Contains(conversationId)
            ? StoreResult<IReadOnlyList<StoredMessage>>.Ok(list.TakeLast(lastN).SkipWhile(m => m.Role != StoredRole.User).ToList())
            : StoreResult<IReadOnlyList<StoredMessage>>.Fail(StoreFailure.NotFound));

    public Task<StoreResult<StoredMessage>> AppendAsync(
        Guid conversationId, StoredRole role, string content, string? toolName, string? toolParamsJson, CancellationToken ct)
    {
        if (!_conversations.TryGetValue(conversationId, out var list) || Foreign.Contains(conversationId))
        {
            return Task.FromResult(StoreResult<StoredMessage>.Fail(StoreFailure.NotFound));
        }

        var message = new StoredMessage(Guid.NewGuid(), list.Count + 1, role, content, toolName, toolParamsJson, DateTime.UtcNow);
        list.Add(message);
        return Task.FromResult(StoreResult<StoredMessage>.Ok(message));
    }

    public Task<IReadOnlyList<ConversationSummary>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ConversationSummary>>([]);

    public Task<StoreResult<IReadOnlyList<StoredMessage>>> OpenAsync(Guid conversationId, CancellationToken ct) =>
        LoadAsync(conversationId, int.MaxValue, ct);

    public Task RecordWarningAsync(Guid conversationId, string eventType, string detailsJson, CancellationToken ct)
    {
        Warnings.Add((conversationId, eventType, detailsJson));
        return Task.CompletedTask;
    }

    /// <summary>Rozmowa należąca do kogoś innego: istnieje, ale użytkownik jej nie widzi.</summary>
    public Guid AddForeignConversation()
    {
        var id = Guid.NewGuid();
        _conversations[id] = new List<StoredMessage>();
        Foreign.Add(id);
        return id;
    }
}
