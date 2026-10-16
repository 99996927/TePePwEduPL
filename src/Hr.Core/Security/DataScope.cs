using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Core.Persistence;

namespace Hr.Core.Security;

/// <summary>
/// Zakres widocznych pracowników wg roli (UC-03, UC-04), budowany jako zapytanie SQL.
/// Brak powiązanego pracownika albo brak rozpoznanej roli daje pusty wynik (fail closed).
/// </summary>
public static class DataScope
{
    public static IQueryable<Guid> VisibleEmployeeIds(this HrDbContext db, ICurrentUser user)
    {
        var none = db.Employees.Where(_ => false).Select(e => e.Id);
        if (user.EmployeeId is not { } me)
        {
            return none;
        }

        if (user.IsInRole(Roles.HR) || user.IsInRole(Roles.HRApprover))
        {
            return db.Employees.Select(e => e.Id);
        }

        var visible = none;
        if (user.IsInRole(Roles.Employee) || user.IsInRole(Roles.Manager))
        {
            visible = visible.Union(db.Employees.Where(e => e.Id == me).Select(e => e.Id));
        }

        if (user.IsInRole(Roles.Manager))
        {
            visible = visible.Union(
                db.Employees
                    .Where(e => db.Teams.Any(t => t.Id == e.TeamId && t.ManagerEmployeeId == me))
                    .Select(e => e.Id));
        }

        return visible;
    }

    public static IQueryable<Employee> VisibleEmployees(this HrDbContext db, ICurrentUser user)
    {
        var ids = db.VisibleEmployeeIds(user);
        return db.Employees.Where(e => ids.Contains(e.Id));
    }

    public static IQueryable<T> Visible<T>(this IQueryable<T> source, HrDbContext db, ICurrentUser user)
        where T : class, IEmployeeOwned
    {
        var ids = db.VisibleEmployeeIds(user);
        return source.Where(x => ids.Contains(x.EmployeeId));
    }
}
