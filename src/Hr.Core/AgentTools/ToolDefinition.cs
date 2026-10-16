namespace Hr.Core.AgentTools;

/// <summary>Opis narzędzia dla modelu: nazwa, opis i schemat JSON parametrów (zależny od roli wywołującego).</summary>
public sealed record ToolDefinition(string Name, string Description, string InputSchemaJson);
