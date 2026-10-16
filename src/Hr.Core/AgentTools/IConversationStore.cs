namespace Hr.Core.AgentTools;

/// <summary>
/// Historia rozmów z agentem (ADR-11). Dopisywać i ładować kontekst można tylko we własnej rozmowie;
/// kadry czytają cudze rozmowy przez <see cref="OpenAsync"/> (z audytem), nigdy do nich nie dopisują.
/// </summary>
public interface IConversationStore
{
    Task<StoreResult<Guid>> StartAsync(CancellationToken ct);

    /// <summary>Ostatnie <paramref name="lastN"/> wiadomości własnej rozmowy; zaczyna się od wiadomości użytkownika, by kontekst był spójny.</summary>
    Task<StoreResult<IReadOnlyList<StoredMessage>>> LoadAsync(Guid conversationId, int lastN, CancellationToken ct);

    /// <summary>Dopisuje wiadomość z kolejnym numerem; równoległy zapis tego samego numeru daje <see cref="StoreFailure.Conflict"/>.</summary>
    Task<StoreResult<StoredMessage>> AppendAsync(
        Guid conversationId, StoredRole role, string content, string? toolName, string? toolParamsJson, CancellationToken ct);

    Task<IReadOnlyList<ConversationSummary>> ListAsync(CancellationToken ct);

    /// <summary>Pełna rozmowa widoczna dla użytkownika; odczyt cudzej zapisuje <c>ConversationRead</c> w audycie.</summary>
    Task<StoreResult<IReadOnlyList<StoredMessage>>> OpenAsync(Guid conversationId, CancellationToken ct);

    Task RecordWarningAsync(Guid conversationId, string eventType, string detailsJson, CancellationToken ct);
}
