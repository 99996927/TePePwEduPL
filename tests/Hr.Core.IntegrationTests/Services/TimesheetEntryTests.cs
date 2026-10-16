using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.IntegrationTests.Services;

[Collection("mssql")]
public class TimesheetEntryTests(MsSqlFixture fixture)
{
    private static DateTime T(int day, int hour, int minute = 0) => new(2026, 10, day, hour, minute, 0);

    private static async Task<WorkEntryRecord> AddAs(ServiceTestContext c, Guid employeeId, int day, int fromHour, int toHour)
    {
        var result = await c.Timesheet(c.As(employeeId, Roles.Employee)).AddEntryAsync(T(day, fromHour), T(day, toHour), TimeSpan.Zero);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    [Fact]
    public async Task UC10_Add_entry_for_self_is_stored()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var result = await c.Timesheet(c.As(c.Org.Anna, Roles.Employee)).AddEntryAsync(T(12, 8), T(12, 17), TimeSpan.FromHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(c.Org.Anna, result.Value!.EmployeeId);
        Assert.Equal(8.0m, result.Value.ToRulesEntry().WorkedHours);
        Assert.Equal(1, await c.Db.WorkEntries.CountAsync());
    }

    [Fact]
    public async Task UC10_Add_entry_with_end_before_start_is_validation_error()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var result = await c.Timesheet(c.As(c.Org.Anna, Roles.Employee)).AddEntryAsync(T(12, 17), T(12, 8), TimeSpan.Zero);

        Assert.Equal(ServiceErrorCode.Validation, result.Error!.Code);
        Assert.Equal(0, await c.Db.WorkEntries.CountAsync());
    }

    [Fact]
    public async Task UC10_Add_entry_with_break_longer_than_entry_is_validation_error()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var result = await c.Timesheet(c.As(c.Org.Anna, Roles.Employee)).AddEntryAsync(T(12, 8), T(12, 9), TimeSpan.FromHours(2));

        Assert.Equal(ServiceErrorCode.Validation, result.Error!.Code);
    }

    [Fact]
    public async Task UC10_Add_entry_ending_in_the_future_is_validation_error()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var result = await c.Timesheet(c.As(c.Org.Anna, Roles.Employee)).AddEntryAsync(T(17, 8), T(17, 16), TimeSpan.Zero);

        Assert.Equal(ServiceErrorCode.Validation, result.Error!.Code);
    }

    [Fact]
    public async Task UC10_Entry_that_ended_earlier_today_in_local_time_is_accepted()
    {
        // Zegar: 12:00 UTC = 14:00 czasu polskiego (CEST); wpis 11:00-13:00 czasu lokalnego już się zakończył.
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var result = await c.Timesheet(c.As(c.Org.Anna, Roles.Employee)).AddEntryAsync(T(16, 11), T(16, 13), TimeSpan.Zero);

        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    [Fact]
    public async Task UC10_Entry_ending_after_local_now_is_validation_error()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var result = await c.Timesheet(c.As(c.Org.Anna, Roles.Employee)).AddEntryAsync(T(16, 13), T(16, 15), TimeSpan.Zero);

        Assert.Equal(ServiceErrorCode.Validation, result.Error!.Code);
    }

    [Fact]
    public async Task UC10_Concurrent_overlapping_adds_create_a_single_entry()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var connectionString = c.Db.Database.GetConnectionString()!;
        var anna = c.As(c.Org.Anna, Roles.Employee);

        foreach (int day in new[] { 5, 6, 7, 8 })
        {
            await using var first = await fixture.OpenContextAsync(connectionString, anna, c.Clock);
            await using var second = await fixture.OpenContextAsync(connectionString, anna, c.Clock);
            var results = await Task.WhenAll(
                new TimesheetService(first, anna, c.Clock, c.Calendar).AddEntryAsync(T(day, 8), T(day, 16), TimeSpan.Zero),
                new TimesheetService(second, anna, c.Clock, c.Calendar).AddEntryAsync(T(day, 8), T(day, 16), TimeSpan.Zero));

            Assert.Equal(1, results.Count(r => r.IsSuccess));
            Assert.Equal(ServiceErrorCode.Conflict, results.Single(r => !r.IsSuccess).Error!.Code);
        }

        Assert.Equal(4, await c.Db.WorkEntries.CountAsync());
    }

    [Fact]
    public async Task UC10_Overlapping_entry_is_conflict_but_adjacent_and_foreign_are_fine()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        await AddAs(c, c.Org.Anna, 12, 8, 16);
        var anna = c.Timesheet(c.As(c.Org.Anna, Roles.Employee));

        var overlapping = await anna.AddEntryAsync(T(12, 12), T(12, 20), TimeSpan.Zero);
        var adjacent = await anna.AddEntryAsync(T(12, 16), T(12, 20), TimeSpan.Zero);
        var foreign = await c.Timesheet(c.As(c.Org.Jan, Roles.Employee)).AddEntryAsync(T(12, 12), T(12, 20), TimeSpan.Zero);

        Assert.Equal(ServiceErrorCode.Conflict, overlapping.Error!.Code);
        Assert.True(adjacent.IsSuccess);
        Assert.True(foreign.IsSuccess);
    }

    [Fact]
    public async Task UC10_Add_without_employee_link_is_forbidden()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var nobody = new TestUser(Guid.NewGuid(), null, Roles.Employee);

        var result = await c.Timesheet(nobody).AddEntryAsync(T(12, 8), T(12, 16), TimeSpan.Zero);

        Assert.Equal(ServiceErrorCode.Forbidden, result.Error!.Code);
    }

    [Fact]
    public async Task UC11_Author_corrects_entry_with_reason_and_audit_records_modification()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var entry = await AddAs(c, c.Org.Anna, 12, 8, 16);

        var result = await c.Timesheet(c.As(c.Org.Anna, Roles.Employee))
            .CorrectEntryAsync(entry.Id, T(12, 8), T(12, 17), TimeSpan.Zero, "Zapomniałam dopisać godzinę");

        Assert.True(result.IsSuccess);
        Assert.Equal(T(12, 17), (await c.Db.WorkEntries.AsNoTracking().SingleAsync()).End);
        Assert.True(await c.Db.AuditLogs.AnyAsync(a => a.EventType == "Modified" && a.EntityType == "WorkEntryRecord"));
        var corrected = await c.Db.AuditLogs.AsNoTracking().SingleAsync(a => a.EventType == "WorkEntryCorrected");
        Assert.Equal(entry.Id.ToString(), corrected.EntityId);
        Assert.Equal("Zapomniałam dopisać godzinę", System.Text.Json.JsonDocument.Parse(corrected.AfterJson!).RootElement.GetProperty("Reason").GetString());
    }

    [Fact]
    public async Task UC11_Correction_without_reason_is_validation_error()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var entry = await AddAs(c, c.Org.Anna, 12, 8, 16);

        var result = await c.Timesheet(c.As(c.Org.Anna, Roles.Employee)).CorrectEntryAsync(entry.Id, T(12, 8), T(12, 17), TimeSpan.Zero, " ");

        Assert.Equal(ServiceErrorCode.Validation, result.Error!.Code);
        Assert.Equal(T(12, 16), (await c.Db.WorkEntries.AsNoTracking().SingleAsync()).End);
    }

    [Fact]
    public async Task UC11_HR_can_correct_any_entry()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var entry = await AddAs(c, c.Org.Anna, 12, 8, 16);

        var result = await c.Timesheet(c.As(c.Org.Piotr, Roles.HR)).CorrectEntryAsync(entry.Id, T(12, 8), T(12, 18), TimeSpan.Zero, "Korekta wg listy obecności");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UC11_Manager_cannot_correct_team_members_entry()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var entry = await AddAs(c, c.Org.Anna, 12, 8, 16);

        var result = await c.Timesheet(c.As(c.Org.Ewa, Roles.Manager)).CorrectEntryAsync(entry.Id, T(12, 8), T(12, 18), TimeSpan.Zero, "Korekta");

        Assert.Equal(ServiceErrorCode.Forbidden, result.Error!.Code);
    }

    [Fact]
    public async Task UC11_Other_employee_gets_not_found()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var entry = await AddAs(c, c.Org.Anna, 12, 8, 16);

        var result = await c.Timesheet(c.As(c.Org.Tomasz, Roles.Employee)).CorrectEntryAsync(entry.Id, T(12, 8), T(12, 18), TimeSpan.Zero, "Korekta");

        Assert.Equal(ServiceErrorCode.NotFound, result.Error!.Code);
    }

    [Fact]
    public async Task UC11_Correction_overlapping_other_entry_is_conflict()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var first = await AddAs(c, c.Org.Anna, 12, 8, 12);
        await AddAs(c, c.Org.Anna, 12, 13, 17);

        var result = await c.Timesheet(c.As(c.Org.Anna, Roles.Employee)).CorrectEntryAsync(first.Id, T(12, 8), T(12, 14), TimeSpan.Zero, "Dłuższy wpis");

        Assert.Equal(ServiceErrorCode.Conflict, result.Error!.Code);
    }

    [Fact]
    public async Task UC03_List_filters_by_range_and_scope()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var annaLate = await AddAs(c, c.Org.Anna, 13, 8, 16);
        var annaEarly = await AddAs(c, c.Org.Anna, 6, 8, 16);
        await AddAs(c, c.Org.Anna, 1, 8, 16);
        var jan = await AddAs(c, c.Org.Jan, 8, 8, 16);
        await AddAs(c, c.Org.Tomasz, 8, 8, 16);

        var asAnna = await c.Timesheet(c.As(c.Org.Anna, Roles.Employee)).ListAsync(new(2026, 10, 5), new(2026, 10, 14));
        var asEwa = await c.Timesheet(c.As(c.Org.Ewa, Roles.Manager)).ListAsync(new(2026, 10, 5), new(2026, 10, 14));

        Assert.Equal(new[] { annaEarly.Id, annaLate.Id }, asAnna.Select(e => e.Id));
        Assert.Equal(new[] { annaEarly.Id, jan.Id, annaLate.Id }, asEwa.Select(e => e.Id));
    }
}
