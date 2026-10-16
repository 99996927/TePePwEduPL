using Hr.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hr.Core.Persistence.Configurations;

public sealed class PayrollChangeConfiguration : IEntityTypeConfiguration<PayrollChange>
{
    public void Configure(EntityTypeBuilder<PayrollChange> builder)
    {
        builder.ToTable("PayrollChanges", t =>
        {
            // ADR-08: zasada czterech oczu egzekwowana w bazie, niezależnie od warstwy serwisów.
            t.HasCheckConstraint("CK_PayrollChange_FourEyes", "[DecidedByUserId] IS NULL OR [DecidedByUserId] <> [CreatedByUserId]");
            t.HasCheckConstraint("CK_PayrollChange_Value", "[Value] >= 0");
            // Zatwierdzenie lub odrzucenie wymaga osoby decydującej; zmiana niezdecydowana jej nie ma.
            t.HasCheckConstraint(
                "CK_PayrollChange_Decision",
                "([Status] IN ('Draft','Pending') AND [DecidedByUserId] IS NULL) OR ([Status] IN ('Approved','Rejected') AND [DecidedByUserId] IS NOT NULL)");
        });
        builder.Property(c => c.Component).HasConversion<string>().HasMaxLength(30);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Value).HasColumnType("decimal(18,2)");
        builder.Property(c => c.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(c => c.DecisionReason).HasMaxLength(1000);
        builder.HasOne<Employee>().WithMany().HasForeignKey(c => c.EmployeeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(c => new { c.EmployeeId, c.EffectiveFrom });
    }
}
