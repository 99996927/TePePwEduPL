using Microsoft.AspNetCore.Identity;

namespace Hr.Core.Domain;

public class ApplicationUser : IdentityUser<Guid>
{
    public Guid? EmployeeId { get; set; }
}
