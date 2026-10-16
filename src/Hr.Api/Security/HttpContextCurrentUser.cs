using System.Security.Claims;
using Hr.Core.Abstractions;

namespace Hr.Api.Security;

/// <summary>Tożsamość wywołującego z claimów cookie. Poza żądaniem HTTP (np. start aplikacji) jest anonimowa.</summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public const string EmployeeIdClaim = "employee_id";

    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? UserId => Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public Guid? EmployeeId => Guid.TryParse(Principal?.FindFirstValue(EmployeeIdClaim), out var id) ? id : null;

    public IReadOnlySet<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet() ?? new HashSet<string>();

    public bool IsInRole(string role) => Roles.Contains(role);
}
