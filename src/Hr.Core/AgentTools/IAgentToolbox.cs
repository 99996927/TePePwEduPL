namespace Hr.Core.AgentTools;

/// <summary>
/// Jedyna droga agenta do danych i reguł (ADR-04, ADR-09): narzędzia działają w kontekście zalogowanego użytkownika,
/// a wyniki są gotowym JSON-em, więc agent nie widzi typów domeny ani silnika reguł.
/// </summary>
public interface IAgentToolbox
{
    IReadOnlyList<ToolDefinition> DefinitionsForCurrentUser();

    /// <summary>Wykonuje narzędzie; nieznane narzędzie, zły JSON i błędy serwisów zwracają wynik błędu, nigdy wyjątek.</summary>
    Task<ToolOutcome> ExecuteAsync(string toolName, string inputJson, Guid? conversationId, CancellationToken ct);
}
