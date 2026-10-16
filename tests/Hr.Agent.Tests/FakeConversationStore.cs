using Hr.Core.AgentTools;

namespace Hr.Agent.Tests;

/// <summary>Magazyn rozmów w pamięci jednego użytkownika; identyfikatory z <see cref="Foreign"/> należą do kogoś innego (NotFound).</summary>
public sealed class FakeConversationStore : IConversationStore
{
    private readonly Dictionary<Guid, List<StoredMessage>> _conversations = new();

    public HashSet<Guid> Foreign { get; } = new();

    /// <summary>Gdy true, zapis pary wywołanie/wynik kończy się konfliktem i nic nie zapisuje.</summary>
    public bool FailToolPairs { get; set; }

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

    public Task<StoreResult<IReadOnlyList<StoredMessage>>> AppendToolPairAsync(
        Guid conversationId, string callId, string toolName, string toolParamsJson, string resultJson, CancellationToken ct)
    {
        if (!_conversations.TryGetValue(conversationId, out var list) || Foreign.Contains(conversationId))
        {
            return Task.FromResult(StoreResult<IReadOnlyList<StoredMessage>>.Fail(StoreFailure.NotFound));
        }

        if (FailToolPairs)
        {
            return Task.FromResult(StoreResult<IReadOnlyList<StoredMessage>>.Fail(StoreFailure.Conflict));
        }

        var call = new StoredMessage(Guid.NewGuid(), list.Count + 1, StoredRole.ToolCall, callId, toolName, toolParamsJson, DateTime.UtcNow);
        var result = new StoredMessage(Guid.NewGuid(), list.Count + 2, StoredRole.ToolResult, resultJson, toolName, null, DateTime.UtcNow);
        list.Add(call);
        list.Add(result);
        return Task.FromResult(StoreResult<IReadOnlyList<StoredMessage>>.Ok([call, result]));
    }

    /// <summary>Wstawia gotową historię (np. uszkodzoną), z pominięciem reguł zapisu.</summary>
    public void Seed(Guid conversationId, params (StoredRole Role, string Content, string? Tool)[] messages)
    {
        var list = _conversations[conversationId];
        foreach (var (role, content, tool) in messages)
        {
            list.Add(new StoredMessage(Guid.NewGuid(), list.Count + 1, role, content, tool, role == StoredRole.ToolCall ? "{}" : null, DateTime.UtcNow));
        }
    }

    public Task<IReadOnlyList<ConversationSummary>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ConversationSummary>>([]);

    public async Task<StoreResult<OpenedConversation>> OpenAsync(Guid conversationId, CancellationToken ct)
    {
        var loaded = await LoadAsync(conversationId, int.MaxValue, ct);
        return loaded.IsSuccess
            ? StoreResult<OpenedConversation>.Ok(new OpenedConversation(conversationId, Guid.Empty, DateTime.UtcNow, loaded.Value!))
            : StoreResult<OpenedConversation>.Fail(StoreFailure.NotFound);
    }

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
