namespace Hr.Core.AgentTools;

/// <summary>Wynik narzędzia jako gotowy JSON (camelCase); błąd to <c>{"error":{"code","message","ruleId"}}</c> z <see cref="IsError"/> = true.</summary>
public sealed record ToolOutcome(string Json, bool IsError);
