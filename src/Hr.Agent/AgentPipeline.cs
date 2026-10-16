using Anthropic;
using Microsoft.Extensions.AI;

namespace Hr.Agent;

public static class AgentPipeline
{
    /// <summary><see cref="ModelGuardChatClient"/> (na zewnątrz), pętla narzędzi SDK z limitem rund, klient modelu.</summary>
    public static IChatClient Build(IChatClient inner, int maxRounds = 6) =>
        new ChatClientBuilder(inner)
            .UseFunctionInvocation(configure: c =>
            {
                c.MaximumIterationsPerRequest = maxRounds;
                c.AllowConcurrentInvocation = false;
            })
            .Use(c => new ModelGuardChatClient(c))
            .Build();

    /// <summary>Oficjalne SDK Anthropic jako <see cref="IChatClient"/>; bez klucza zwraca klienta, który nigdy nie łączy się z siecią.</summary>
    public static IChatClient CreateAnthropic(AnthropicOptions options)
    {
        string? key = string.IsNullOrWhiteSpace(options.ApiKey) ? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") : options.ApiKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            return new MissingKeyChatClient();
        }

        var client = new AnthropicClient { ApiKey = key, MaxRetries = options.MaxRetries, Timeout = options.Timeout };
        return client.AsIChatClient(options.Model, options.MaxTokens);
    }

    private sealed class MissingKeyChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new ModelUnavailableException("Brak klucza API Anthropic (Anthropic:ApiKey albo ANTHROPIC_API_KEY).");

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new ModelUnavailableException("Brak klucza API Anthropic (Anthropic:ApiKey albo ANTHROPIC_API_KEY).");

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
