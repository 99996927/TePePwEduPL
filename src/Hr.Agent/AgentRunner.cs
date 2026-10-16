using System.Text.Json;
using Hr.Core.AgentTools;
using Microsoft.Extensions.AI;

namespace Hr.Agent;

/// <summary>
/// Jedno pytanie do agenta: kontekst z historii, pętla narzędzi SDK, zapis rozmowy i osłona liczb (wyzwanie projektu).
/// Agent nie liczy sam: liczby w odpowiedzi muszą pochodzić z wyników narzędzi, a przy odstępstwie model dostaje jedną rundę korekty.
/// </summary>
public sealed class AgentRunner(IChatClient chat, IAgentToolbox toolbox, IConversationStore store)
{
    public const int MaxQuestionLength = 4000;
    public const int HistoryMessages = 40;
    public const string FallbackText =
        "Nie udało mi się ustalić odpowiedzi w dozwolonej liczbie kroków. Spróbuj zadać pytanie inaczej albo skontaktuj się z działem kadr.";

    public async Task<AgentResult> AskAsync(Guid? conversationId, string question, CancellationToken ct)
    {
        question = question?.Trim() ?? string.Empty;
        if (question.Length == 0)
        {
            return AgentResult.Fail(AgentFailure.EmptyQuestion);
        }

        if (question.Length > MaxQuestionLength)
        {
            return AgentResult.Fail(AgentFailure.TooLong);
        }

        IReadOnlyList<StoredMessage> history = [];
        Guid id;
        if (conversationId is { } existing)
        {
            var loaded = await store.LoadAsync(existing, HistoryMessages, ct);
            if (!loaded.IsSuccess)
            {
                return AgentResult.Fail(Map(loaded.Failure!.Value));
            }

            id = existing;
            history = loaded.Value!;
        }
        else
        {
            var started = await store.StartAsync(ct);
            if (!started.IsSuccess)
            {
                return AgentResult.Fail(Map(started.Failure!.Value));
            }

            id = started.Value;
        }

        var newMessages = new List<StoredMessage>();
        StoreFailure? appendFailure = null;

        async Task<bool> AppendAsync(StoredRole role, string content, string? toolName = null, string? toolParams = null)
        {
            var appended = await store.AppendAsync(id, role, content, toolName, toolParams, ct);
            if (appended.IsSuccess)
            {
                newMessages.Add(appended.Value!);
                return true;
            }

            appendFailure ??= appended.Failure;
            return false;
        }

        if (!await AppendAsync(StoredRole.User, question))
        {
            return AgentResult.Fail(Map(appendFailure!.Value), id);
        }

        var toolResults = new List<string>();
        var functions = ToolFunctionAdapter.For(toolbox, () => id, async (callId, tool, input, result) =>
        {
            // Para wywołanie/wynik jest zapisywana w jednym zapisie po wykonaniu narzędzia, więc nigdy nie powstaje samo wywołanie.
            var pair = await store.AppendToolPairAsync(id, callId, tool, input, result, ct);
            if (pair.IsSuccess)
            {
                newMessages.AddRange(pair.Value!);
            }
            else
            {
                appendFailure ??= pair.Failure;
            }

            toolResults.Add(result);
        });
        var options = new ChatOptions { Instructions = SystemPrompt.Text, Tools = functions.Cast<AITool>().ToList() };

        var messages = ChatHistory.Rebuild(history);
        messages.Add(new ChatMessage(ChatRole.User, question));
        var sources = history.Where(m => m.Role is StoredRole.ToolResult or StoredRole.User).Select(m => m.Content)
            .Append(question).ToList();

        string text;
        IReadOnlyList<string> ungrounded;
        try
        {
            var response = await chat.GetResponseAsync(messages, options, ct);
            text = TextOf(response);
            ungrounded = NumberGuard.FindUngrounded(text, sources.Concat(toolResults));
            if (ungrounded.Count > 0)
            {
                messages.AddRange(response.Messages);
                messages.Add(new ChatMessage(ChatRole.User, CorrectionPrompt(ungrounded)));
                response = await chat.GetResponseAsync(messages, options, ct);
                text = TextOf(response);
                ungrounded = NumberGuard.FindUngrounded(text, sources.Concat(toolResults));
            }
        }
        catch (ModelUnavailableException)
        {
            return AgentResult.Fail(appendFailure is { } failure ? Map(failure) : AgentFailure.Unavailable, id);
        }

        if (appendFailure is { } toolFailure)
        {
            return AgentResult.Fail(Map(toolFailure), id);
        }

        if (!await AppendAsync(StoredRole.Assistant, text))
        {
            return AgentResult.Fail(Map(appendFailure!.Value), id);
        }

        if (ungrounded.Count > 0)
        {
            await store.RecordWarningAsync(id, "AgentUngroundedNumbers", JsonSerializer.Serialize(new { numbers = ungrounded }), ct);
        }

        return AgentResult.Ok(new AgentReply(id, text, newMessages, ungrounded));
    }

    private static string TextOf(ChatResponse response) =>
        string.IsNullOrWhiteSpace(response.Text) ? FallbackText : response.Text.Trim();

    private static string CorrectionPrompt(IReadOnlyList<string> numbers) =>
        $"W Twojej odpowiedzi są liczby, których nie ma w wynikach narzędzi: {string.Join(", ", numbers)}. " +
        "Odpowiedz ponownie, używając wyłącznie liczb z wyników narzędzi (możesz wywołać narzędzie), albo poinformuj, że danych brak. " +
        "Niczego nie szacuj, nie sumuj ani nie przeliczaj.";

    private static AgentFailure Map(StoreFailure failure) =>
        failure == StoreFailure.Conflict ? AgentFailure.Conflict : AgentFailure.NotFound;
}
