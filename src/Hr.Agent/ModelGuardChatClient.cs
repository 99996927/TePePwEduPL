using Anthropic.Exceptions;
using Microsoft.Extensions.AI;

namespace Hr.Agent;

/// <summary>Zamienia awarie transportu i SDK na <see cref="ModelUnavailableException"/> (Q-05); anulowanie przez wywołującego przechodzi bez zmian.</summary>
public sealed class ModelGuardChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.GetResponseAsync(messages, options, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is AnthropicException or HttpRequestException or TimeoutException or OperationCanceledException)
        {
            // Bez ex.Message i bez wyjątku wewnętrznego: mogłyby zawierać treść żądania albo nagłówki z kluczem.
            throw new ModelUnavailableException($"Model jest niedostępny ({ex.GetType().Name}).");
        }
    }

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Odpowiedź w całości, bez strumieniowania (ADR-14).");
}
