using Hr.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hr.Core.Persistence.Configurations;

public sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("LeaveRequests", t =>
        {
            t.HasCheckConstraint("CK_LeaveRequest_Period", "[To] >= [From]");
            t.HasCheckConstraint("CK_LeaveRequest_Days", "[Days] > 0");
        });
        builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.DecisionReason).HasMaxLength(1000);
        builder.HasOne<Employee>().WithMany().HasForeignKey(l => l.EmployeeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Employee>().WithMany().HasForeignKey(l => l.DecidedByEmployeeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(l => new { l.EmployeeId, l.From });
    }
}

public sealed class WorkEntryRecordConfiguration : IEntityTypeConfiguration<WorkEntryRecord>
{
    public void Configure(EntityTypeBuilder<WorkEntryRecord> builder)
    {
        builder.ToTable("WorkEntries", t =>
        {
            t.HasCheckConstraint("CK_WorkEntry_Period", "[End] > [Start]");
            t.HasCheckConstraint("CK_WorkEntry_Break", "[Break] >= '00:00:00'");
        });
        builder.HasOne<Employee>().WithMany().HasForeignKey(w => w.EmployeeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(w => new { w.EmployeeId, w.Start });
    }
}
