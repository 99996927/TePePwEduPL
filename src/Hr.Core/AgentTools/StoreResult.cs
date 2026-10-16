namespace Hr.Core.AgentTools;

public enum StoreFailure
{
    NotFound,
    Conflict,
}

/// <summary>Wynik operacji magazynu rozmów. Własny typ w przestrzeni <c>AgentTools</c>, bo agent nie może używać <c>Hr.Core.Services</c>.</summary>
public sealed class StoreResult<T>
{
    private StoreResult(T? value, StoreFailure? failure)
    {
        Value = value;
        Failure = failure;
    }

    public bool IsSuccess => Failure is null;

    public T? Value { get; }

    public StoreFailure? Failure { get; }

    public static StoreResult<T> Ok(T value) => new(value, null);

    public static StoreResult<T> Fail(StoreFailure failure) => new(default, failure);
}
