using System.Security.Claims;
using Hr.Core.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Hr.Api.Security;

/// <summary>Dodaje do tożsamości claim <c>employee_id</c> powiązanego pracownika.</summary>
public sealed class HrClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<Guid>>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (user.EmployeeId is { } employeeId)
        {
            identity.AddClaim(new Claim(HttpContextCurrentUser.EmployeeIdClaim, employeeId.ToString()));
        }

        return identity;
    }
}
