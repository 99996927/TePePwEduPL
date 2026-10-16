namespace Hr.Core.Security;

/// <summary>Dane należące do konkretnego pracownika, filtrowane przez <c>DataScope</c>.</summary>
public interface IEmployeeOwned
{
    Guid EmployeeId { get; }
}
