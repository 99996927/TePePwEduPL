using Hr.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hr.Core.Persistence.Configurations;

public sealed class PayrollResultConfiguration : IEntityTypeConfiguration<PayrollResultRecord>
{
    public void Configure(EntityTypeBuilder<PayrollResultRecord> builder)
    {
        builder.ToTable("PayrollResults", t => t.HasCheckConstraint("CK_PayrollResult_Month", "[Month] BETWEEN 1 AND 12"));
        foreach (var amount in new[]
        {
            nameof(PayrollResultRecord.BaseSalary), nameof(PayrollResultRecord.FixedAllowance), nameof(PayrollResultRecord.NormHours),
            nameof(PayrollResultRecord.Overtime50Hours), nameof(PayrollResultRecord.Overtime100Hours),
            nameof(PayrollResultRecord.NormalOvertimePay), nameof(PayrollResultRecord.Allowance50),
            nameof(PayrollResultRecord.Allowance100), nameof(PayrollResultRecord.Gross),
        })
        {
            builder.Property(amount).HasColumnType("decimal(18,2)");
        }

        builder.Property(r => r.RuleIds).HasMaxLength(200).IsRequired();
        builder.HasOne<Employee>().WithMany().HasForeignKey(r => r.EmployeeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(r => new { r.EmployeeId, r.Year, r.Month }).IsUnique();
    }
}
