using Hr.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hr.Core.Persistence.Configurations;

public sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.HasOne<Employee>().WithMany().HasForeignKey(t => t.ManagerEmployeeId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.LastName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.MonthlySalary).HasColumnType("decimal(18,2)");
        builder.HasOne<Team>().WithMany().HasForeignKey(e => e.TeamId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.HasOne<Employee>().WithMany().HasForeignKey(u => u.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    }
}
