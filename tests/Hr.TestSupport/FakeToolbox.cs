using Hr.Core.AgentTools;

namespace Hr.TestSupport;

/// <summary>Narzędzia w pamięci: zliczają wywołania i zwracają przygotowane wyniki.</summary>
public sealed class FakeToolbox : IAgentToolbox
{
    public List<ToolDefinition> Definitions { get; } = new()
    {
        new("get_leave_balance", "Saldo urlopu.", "{\"type\":\"object\",\"properties\":{}}"),
        new("calculate_overtime", "Nadgodziny.", "{\"type\":\"object\",\"properties\":{\"from\":{\"type\":\"string\"},\"to\":{\"type\":\"string\"}},\"required\":[\"from\",\"to\"]}"),
    };

    public Dictionary<string, ToolOutcome> Results { get; } = new();

    public List<(string Tool, string Input, Guid? ConversationId)> Calls { get; } = new();

    public IReadOnlyList<ToolDefinition> DefinitionsForCurrentUser() => Definitions;

    public Task<ToolOutcome> ExecuteAsync(string toolName, string inputJson, Guid? conversationId, CancellationToken ct)
    {
        Calls.Add((toolName, inputJson, conversationId));
        return Task.FromResult(Results.TryGetValue(toolName, out var outcome)
            ? outcome
            : new ToolOutcome("{\"error\":{\"code\":\"UnknownTool\",\"message\":\"Nieznane narzędzie.\"}}", true));
    }
}
