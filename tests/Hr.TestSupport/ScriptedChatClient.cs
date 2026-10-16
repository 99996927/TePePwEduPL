using Microsoft.Extensions.AI;

namespace Hr.TestSupport;

/// <summary>
/// Skryptowany model: kolejne wywołania <see cref="GetResponseAsync"/> zwracają przygotowane odpowiedzi (albo rzucają wyjątek),
/// a otrzymane wiadomości są zapamiętywane. Pętlę narzędzi prowadzi prawdziwy <c>FunctionInvokingChatClient</c> nad tym klientem.
/// </summary>
public sealed class ScriptedChatClient : IChatClient
{
    private readonly Queue<Func<IReadOnlyList<ChatMessage>, ChatResponse>> _script = new();

    public List<IReadOnlyList<ChatMessage>> Requests { get; } = new();

    public List<ChatOptions?> Options { get; } = new();

    /// <summary>Po wyczerpaniu skryptu klient powtarza ostatnią odpowiedź (przydatne do pętli bez końca).</summary>
    public bool RepeatLast { get; set; }

    private Func<IReadOnlyList<ChatMessage>, ChatResponse>? _last;

    public ScriptedChatClient Then(ChatResponse response) => Then(_ => response);

    public ScriptedChatClient Then(Func<IReadOnlyList<ChatMessage>, ChatResponse> response)
    {
        _script.Enqueue(response);
        return this;
    }

    public ScriptedChatClient ThenText(string text) => Then(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));

    public ScriptedChatClient ThenToolCall(string callId, string name, IDictionary<string, object?>? arguments = null) =>
        Then(new ChatResponse(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(callId, name, arguments)])));

    public ScriptedChatClient ThenThrow(Exception exception) =>
        Then(_ => throw exception);

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = messages.ToList();
        Requests.Add(snapshot);
        Options.Add(options);
        Func<IReadOnlyList<ChatMessage>, ChatResponse> next;
        if (_script.Count > 0)
        {
            next = _last = _script.Dequeue();
        }
        else if (RepeatLast && _last is not null)
        {
            next = _last;
        }
        else
        {
            throw new InvalidOperationException("Skrypt modelu wyczerpany: nieoczekiwane wywołanie.");
        }

        return Task.FromResult(next(snapshot));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Odpowiedź w całości (ADR-14).");

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }
}
