using Hr.Core.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.Persistence;

public class HrDbContext(DbContextOptions<HrDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Team> Teams => Set<Team>();

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();

    public DbSet<WorkEntryRecord> WorkEntries => Set<WorkEntryRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(HrDbContext).Assembly);
    }
}
