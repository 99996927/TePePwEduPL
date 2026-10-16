namespace Hr.Core.Services;

public enum ServiceErrorCode
{
    NotFound,
    Forbidden,
    Validation,
    Conflict,
}

public sealed record ServiceError(ServiceErrorCode Code, string Message, string? RuleId = null);

public sealed class ServiceResult<T>
{
    private ServiceResult(T? value, ServiceError? error)
    {
        Value = value;
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public T? Value { get; }

    public ServiceError? Error { get; }

    public static ServiceResult<T> Ok(T value) => new(value, null);

    public static ServiceResult<T> Fail(ServiceErrorCode code, string message, string? ruleId = null) =>
        new(default, new ServiceError(code, message, ruleId));
}
