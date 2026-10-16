namespace Hr.Agent;

/// <summary>Konfiguracja klienta Anthropic (sekcja <c>Anthropic</c>); klucz tylko z konfiguracji lub zmiennej <c>ANTHROPIC_API_KEY</c>.</summary>
public sealed class AnthropicOptions
{
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "claude-sonnet-5-5";

    public int MaxTokens { get; set; } = 1024;

    public int MaxRetries { get; set; } = 2;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);
}
