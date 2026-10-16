using System.Text.Json;
using Hr.Core.AgentTools;
using Microsoft.Extensions.AI;

namespace Hr.Agent;

/// <summary>Udostępnia narzędzia z <see cref="IAgentToolbox"/> jako <see cref="AIFunction"/>; pętlę „model, narzędzie, model” prowadzi SDK.</summary>
public static class ToolFunctionAdapter
{
    /// <param name="onInvoked">Wołany po wykonaniu narzędzia: (identyfikator wywołania, narzędzie, parametry JSON, wynik JSON). Jedyny punkt zapisu par wywołanie/wynik.</param>
    public static IReadOnlyList<AIFunction> For(
        IAgentToolbox toolbox, Func<Guid?> conversationId, Func<string, string, string, string, Task> onInvoked) =>
        toolbox.DefinitionsForCurrentUser()
            .Select(d => (AIFunction)new ToolFunction(d, toolbox, conversationId, onInvoked))
            .ToList();

    private sealed class ToolFunction(
        ToolDefinition definition, IAgentToolbox toolbox, Func<Guid?> conversationId, Func<string, string, string, string, Task> onInvoked) : AIFunction
    {
        private readonly JsonElement _schema = JsonDocument.Parse(definition.InputSchemaJson).RootElement.Clone();

        public override string Name => definition.Name;

        public override string Description => definition.Description;

        public override JsonElement JsonSchema => _schema;

        protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            string inputJson = JsonSerializer.Serialize(arguments.ToDictionary(a => a.Key, a => a.Value));
            var outcome = await toolbox.ExecuteAsync(definition.Name, inputJson, conversationId(), cancellationToken);
            string callId = FunctionInvokingChatClient.CurrentContext?.CallContent.CallId ?? Guid.NewGuid().ToString("N");
            await onInvoked(callId, definition.Name, inputJson, outcome.Json);
            // Wynik wraca do modelu jako surowy JSON (z właściwością „error” dla błędu narzędzia), nie jako tekst w cudzysłowie.
            return JsonDocument.Parse(outcome.Json).RootElement.Clone();
        }
    }
}
