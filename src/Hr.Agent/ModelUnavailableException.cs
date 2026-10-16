namespace Hr.Agent;

/// <summary>Model jest niedostępny (brak klucza, sieć, limit, błąd serwera); komunikat nie zawiera treści żądania ani klucza.</summary>
public sealed class ModelUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
