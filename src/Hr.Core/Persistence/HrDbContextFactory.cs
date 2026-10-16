using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hr.Core.Persistence;

/// <summary>Fabryka używana wyłącznie przez narzędzie dotnet-ef do generowania migracji.</summary>
public sealed class HrDbContextFactory : IDesignTimeDbContextFactory<HrDbContext>
{
    public HrDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("HR_DESIGN_TIME_CONNECTION")
            ?? "Server=localhost,1433;Database=hr_design;User Id=sa;Password=Design!Time1;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<HrDbContext>().UseSqlServer(connectionString).Options;
        return new HrDbContext(options);
    }
}
