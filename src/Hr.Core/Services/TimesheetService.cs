using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Core.Persistence;
using Hr.Core.Security;
using Hr.Rules.Calendar;
using Hr.Rules.Time;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.Services;

/// <summary>Ewidencja czasu pracy (UC-10, UC-11) i jej analiza (UC-12 do UC-15). Liczby pochodzą z <c>Hr.Rules</c>.</summary>
public sealed class TimesheetService(HrDbContext db, ICurrentUser user, IClock clock, WorkCalendar calendar)
{
    internal WorkCalendar Calendar => calendar;

    /// <summary>Dodaje wpis dla siebie. Wpis nie może się nakładać na inny ani kończyć w przyszłości.</summary>
    public async Task<ServiceResult<WorkEntryRecord>> AddEntryAsync(DateTime start, DateTime end, TimeSpan breakTime, CancellationToken ct = default)
    {
        if (user.EmployeeId is not { } employeeId || !Roles.All.Any(user.IsInRole))
        {
            return ServiceResult<WorkEntryRecord>.Fail(ServiceErrorCode.Forbidden, "Użytkownik nie jest powiązany z pracownikiem.");
        }

        var invalid = Validate(start, end, breakTime);
        if (invalid is not null)
        {
            return ServiceResult<WorkEntryRecord>.Fail(ServiceErrorCode.Validation, invalid);
        }

        if (await OverlapsAsync(employeeId, start, end, null, ct))
        {
            return ServiceResult<WorkEntryRecord>.Fail(ServiceErrorCode.Conflict, "Wpis nakłada się na inny wpis czasu pracy.");
        }

        var entry = new WorkEntryRecord { Id = Guid.NewGuid(), EmployeeId = employeeId, Start = start, End = end, Break = breakTime };
        db.WorkEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return ServiceResult<WorkEntryRecord>.Ok(entry);
    }

    /// <summary>Koryguje wpis z uzasadnieniem (UC-11): autor albo kadry; zmiana trafia do audytu.</summary>
    public async Task<ServiceResult<WorkEntryRecord>> CorrectEntryAsync(
        Guid entryId, DateTime start, DateTime end, TimeSpan breakTime, string reason, CancellationToken ct = default)
    {
        var entry = await db.WorkEntries.Visible(db, user).FirstOrDefaultAsync(w => w.Id == entryId, ct);
        if (entry is null)
        {
            return ServiceResult<WorkEntryRecord>.Fail(ServiceErrorCode.NotFound, "Nie znaleziono wpisu.");
        }

        bool allowed = entry.EmployeeId == user.EmployeeId || user.IsInRole(Roles.HR) || user.IsInRole(Roles.HRApprover);
        if (!allowed)
        {
            return ServiceResult<WorkEntryRecord>.Fail(ServiceErrorCode.Forbidden, "Brak uprawnień do korekty tego wpisu.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return ServiceResult<WorkEntryRecord>.Fail(ServiceErrorCode.Validation, "Korekta wymaga uzasadnienia.");
        }

        var invalid = Validate(start, end, breakTime);
        if (invalid is not null)
        {
            return ServiceResult<WorkEntryRecord>.Fail(ServiceErrorCode.Validation, invalid);
        }

        if (await OverlapsAsync(entry.EmployeeId, start, end, entry.Id, ct))
        {
            return ServiceResult<WorkEntryRecord>.Fail(ServiceErrorCode.Conflict, "Wpis nakłada się na inny wpis czasu pracy.");
        }

        entry.Start = start;
        entry.End = end;
        entry.Break = breakTime;
        await db.SaveChangesAsync(ct);
        return ServiceResult<WorkEntryRecord>.Ok(entry);
    }

    /// <summary>Widoczne wpisy o dacie rozpoczęcia w przedziale, rosnąco po początku.</summary>
    public async Task<IReadOnlyList<WorkEntryRecord>> ListAsync(DateOnly from, DateOnly toInclusive, CancellationToken ct = default)
    {
        var rangeStart = from.ToDateTime(TimeOnly.MinValue);
        var rangeEnd = toInclusive.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return await db.WorkEntries.Visible(db, user)
            .Where(w => w.Start >= rangeStart && w.Start < rangeEnd)
            .OrderBy(w => w.Start)
            .ToListAsync(ct);
    }

    private string? Validate(DateTime start, DateTime end, TimeSpan breakTime)
    {
        try
        {
            _ = new WorkEntry(start, end, breakTime);
        }
        catch (ArgumentException ex)
        {
            return ex.Message;
        }

        return end > clock.UtcNow ? "Wpis nie może kończyć się w przyszłości." : null;
    }

    private Task<bool> OverlapsAsync(Guid employeeId, DateTime start, DateTime end, Guid? excludeId, CancellationToken ct) =>
        db.WorkEntries.AnyAsync(w => w.EmployeeId == employeeId && w.Id != excludeId && w.Start < end && w.End > start, ct);
}
