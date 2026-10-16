using Anthropic.Exceptions;
using Hr.Agent;
using Hr.TestSupport;
using Microsoft.Extensions.AI;

namespace Hr.Agent.Tests;

public class ModelGuardChatClientTests
{
    private static Task<ChatResponse> Ask(IChatClient client, CancellationToken ct = default) =>
        client.GetResponseAsync([new ChatMessage(ChatRole.User, "Cześć")], null, ct);

    [Fact]
    public async Task HttpRequestException_and_timeout_become_ModelUnavailable()
    {
        foreach (Exception failure in new Exception[] { new HttpRequestException("połączenie"), new TimeoutException(), new TaskCanceledException("upłynął czas") })
        {
            var guard = new ModelGuardChatClient(new ScriptedChatClient().ThenThrow(failure));

            await Assert.ThrowsAsync<ModelUnavailableException>(() => Ask(guard));
        }
    }

    [Fact]
    public async Task Anthropic_api_exception_becomes_ModelUnavailable()
    {
        var guard = new ModelGuardChatClient(new ScriptedChatClient().ThenThrow(new AnthropicException("429 Too Many Requests")));

        await Assert.ThrowsAsync<ModelUnavailableException>(() => Ask(guard));
    }

    [Fact]
    public async Task Caller_cancellation_is_not_converted()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var guard = new ModelGuardChatClient(new ScriptedChatClient().ThenText("nie dotrze"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Ask(guard, cts.Token));
    }

    [Fact]
    public async Task Message_never_contains_api_key()
    {
        const string secret = "sk-ant-SEKRET-123";
        var failure = new HttpRequestException($"401 x-api-key: {secret}", new InvalidOperationException(secret));
        var guard = new ModelGuardChatClient(new ScriptedChatClient().ThenThrow(failure));

        var ex = await Assert.ThrowsAsync<ModelUnavailableException>(() => Ask(guard));

        Assert.DoesNotContain(secret, ex.ToString());
    }
}
