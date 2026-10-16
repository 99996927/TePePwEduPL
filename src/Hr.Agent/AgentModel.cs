namespace Hr.Agent;

public static class AgentModel
{
    /// <summary>Klucz rejestracji „surowego” klienta modelu w DI; pętla narzędzi i osłona są dobudowywane nad nim przez <see cref="AgentPipeline.Build"/>.</summary>
    public const string ServiceKey = "agent-model";
}
