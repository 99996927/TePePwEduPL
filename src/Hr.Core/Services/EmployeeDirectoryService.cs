using Hr.Core.Abstractions;
using Hr.Core.Persistence;
using Hr.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.Services;

/// <summary>Skrót pracownika do list i list wyboru w interfejsie; celowo bez wynagrodzenia.</summary>
public sealed record EmployeeSummary(Guid Id, string FirstName, string LastName, Guid TeamId, string TeamName, DateOnly HireDate);

/// <summary>Lista pracowników widocznych dla użytkownika wg <see cref="DataScope"/> (własne, zespół, wszyscy).</summary>
public sealed class EmployeeDirectoryService(HrDbContext db, ICurrentUser user)
{
    public async Task<IReadOnlyList<EmployeeSummary>> ListAsync(CancellationToken ct = default)
    {
        var visible = db.VisibleEmployeeIds(user);
        return await (
            from e in db.Employees
            where visible.Contains(e.Id)
            join t in db.Teams on e.TeamId equals t.Id
            orderby e.LastName, e.FirstName
            select new EmployeeSummary(e.Id, e.FirstName, e.LastName, e.TeamId, t.Name, e.HireDate))
            .ToListAsync(ct);
    }
}
