using Hr.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hr.Core.Persistence.Configurations;

public sealed class CorrectionDraftConfiguration : IEntityTypeConfiguration<CorrectionDraft>
{
    public void Configure(EntityTypeBuilder<CorrectionDraft> builder)
    {
        builder.ToTable("CorrectionDrafts", t => t.HasCheckConstraint("CK_CorrectionDraft_Period", "[ProposedEnd] > [ProposedStart]"));
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(d => d.DecisionNote).HasMaxLength(1000);
        builder.HasOne<Employee>().WithMany().HasForeignKey(d => d.EmployeeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<WorkEntryRecord>().WithMany().HasForeignKey(d => d.WorkEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(d => new { d.Status, d.CreatedAtUtc });
    }
}
