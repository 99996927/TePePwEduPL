using System.Text.Json;
using Hr.Core.AgentTools;
using Microsoft.Extensions.AI;

namespace Hr.Agent;

/// <summary>Odbudowuje kontekst modelu z zapisanej rozmowy: pary wywołanie/wynik wracają jako <see cref="FunctionCallContent"/> i <see cref="FunctionResultContent"/>.</summary>
internal static class ChatHistory
{
    public static List<ChatMessage> Rebuild(IReadOnlyList<StoredMessage> history)
    {
        var messages = new List<ChatMessage>();
        for (int i = 0; i < history.Count; i++)
        {
            var message = history[i];
            switch (message.Role)
            {
                case StoredRole.User:
                    messages.Add(new ChatMessage(ChatRole.User, message.Content));
                    break;
                case StoredRole.Assistant:
                    messages.Add(new ChatMessage(ChatRole.Assistant, message.Content));
                    break;
                case StoredRole.ToolCall:
                    // Samotne wywołanie (bez wyniku, np. po przerwanym zapisie) jest pomijane: model nie może dostać tool_use bez tool_result.
                    var calls = new List<AIContent>();
                    var results = new List<AIContent>();
                    // Kolejne kompletne pary (wywołanie, wynik) trafiają do jednej wiadomości assistant i jednej tool.
                    while (i + 1 < history.Count && history[i].Role == StoredRole.ToolCall && history[i + 1].Role == StoredRole.ToolResult)
                    {
                        var call = history[i];
                        calls.Add(new FunctionCallContent(call.Content, call.ToolName ?? string.Empty, Arguments(call.ToolParamsJson)));
                        results.Add(new FunctionResultContent(call.Content, ParseResult(history[i + 1].Content)));
                        i += 2;
                    }

                    if (calls.Count > 0)
                    {
                        // Następna iteracja pętli „for” ma wziąć pierwszy nieprzetworzony element.
                        i--;
                        messages.Add(new ChatMessage(ChatRole.Assistant, calls));
                        messages.Add(new ChatMessage(ChatRole.Tool, results));
                    }

                    break;
            }
        }

        return messages;
    }

    private static Dictionary<string, object?>? Arguments(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static object ParseResult(string json)
    {
        try
        {
            return JsonDocument.Parse(json).RootElement.Clone();
        }
        catch (JsonException)
        {
            return json;
        }
    }
}
