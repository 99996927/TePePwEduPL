using System.Security.Claims;
using Hr.Core.Domain;
using Hr.Core.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Hr.Api.Security;

/// <summary>Dodaje do tożsamości claimy <c>employee_id</c> i <c>display_name</c> powiązanego pracownika.</summary>
public sealed class HrClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOptions<IdentityOptions> options,
    HrDbContext db)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<Guid>>(userManager, roleManager, options)
{
    public const string DisplayNameClaim = "display_name";

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (user.EmployeeId is { } employeeId)
        {
            identity.AddClaim(new Claim(HttpContextCurrentUser.EmployeeIdClaim, employeeId.ToString()));
            var name = await db.Employees.Where(e => e.Id == employeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefaultAsync();
            if (name is not null)
            {
                identity.AddClaim(new Claim(DisplayNameClaim, name));
            }
        }

        return identity;
    }
}
