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

    public DbSet<PayrollChange> PayrollChanges => Set<PayrollChange>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<PayrollResultRecord> PayrollResults => Set<PayrollResultRecord>();

    public DbSet<CorrectionDraft> CorrectionDrafts => Set<CorrectionDraft>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(HrDbContext).Assembly);
    }
}
