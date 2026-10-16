using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Core.Services;

namespace Hr.Core.IntegrationTests.Services;

[Collection("mssql")]
public class TimesheetAnalysisTests(MsSqlFixture fixture)
{
    private static WorkEntryRecord Entry(Guid employeeId, DateTime start, DateTime end) => new()
    {
        Id = Guid.NewGuid(), EmployeeId = employeeId, Start = start, End = end, Break = TimeSpan.Zero,
    };

    private static DateTime T(int month, int day, int hour) => new(2026, month, day, hour, 0, 0);

    private static async Task AddAsync(ServiceTestContext c, params WorkEntryRecord[] entries)
    {
        c.Db.WorkEntries.AddRange(entries);
        await c.Db.SaveChangesAsync();
    }

    private static async Task<TimesheetAnalysis> AnalyzeAsync(ServiceTestContext c, Guid employeeId, DateOnly from, DateOnly to)
    {
        var result = await c.Timesheet(c.As(c.Org.Piotr, Roles.HR)).AnalyzeAsync(employeeId, from, to);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    [Fact]
    public async Task UC12_Daily_rest_violation_is_reported()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        await AddAsync(c, Entry(c.Org.Anna, T(10, 12, 8), T(10, 12, 22)), Entry(c.Org.Anna, T(10, 13, 8), T(10, 13, 16)));

        var analysis = await AnalyzeAsync(c, c.Org.Anna, new(2026, 10, 12), new(2026, 10, 18));

        var violation = Assert.Single(analysis.RestViolations, v => v.RuleId == "KP-132-1");
        Assert.Equal(10m, violation.RestHours);
    }

    [Fact]
    public async Task UC13_Weekly_rest_violation_is_reported()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        await AddAsync(c, Enumerable.Range(12, 7).Select(d => Entry(c.Org.Anna, T(10, d, 8), T(10, d, 16))).ToArray());

        var analysis = await AnalyzeAsync(c, c.Org.Anna, new(2026, 10, 12), new(2026, 10, 18));

        Assert.Contains(analysis.RestViolations, v => v.RuleId == "KP-133-1");
    }

    [Fact]
    public async Task UC14_Average_over_48h_is_flagged()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var entries = Enumerable.Range(0, 28)
            .Select(i => new DateTime(2026, 10, 5).AddDays(i))
            .Where(d => d.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday)
            .Select(d => Entry(c.Org.Anna, d.AddHours(8), d.AddHours(18)))
            .ToArray();
        await AddAsync(c, entries);

        var analysis = await AnalyzeAsync(c, c.Org.Anna, new(2026, 10, 5), new(2026, 11, 1));

        Assert.Equal(50m, analysis.WeeklyAverage.AverageWeeklyHours);
        Assert.True(analysis.WeeklyAverage.ExceedsLimit);
        Assert.Equal("KP-131-1", analysis.WeeklyAverage.RuleId);
    }

    [Fact]
    public async Task UC10_Overtime_comes_from_engine()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        await AddAsync(c, Entry(c.Org.Anna, T(10, 12, 8), T(10, 12, 18)));

        var analysis = await AnalyzeAsync(c, c.Org.Anna, new(2026, 10, 12), new(2026, 10, 18));

        Assert.Equal(2m, analysis.Overtime.Hours50);
        Assert.Equal(10m, analysis.WorkedHours);
    }

    [Fact]
    public async Task UC14_Norm_hours_for_october_2026_is_176_and_drops_with_approved_leave()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        Assert.Equal(176m, (await AnalyzeAsync(c, c.Org.Anna, new(2026, 10, 1), new(2026, 10, 31))).NormHours);

        c.Db.LeaveRequests.Add(new LeaveRequest
        {
            Id = Guid.NewGuid(), EmployeeId = c.Org.Anna, From = new(2026, 10, 12), To = new(2026, 10, 16), Days = 5,
            Status = LeaveStatus.Approved, CreatedAtUtc = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc),
        });
        await c.Db.SaveChangesAsync();

        Assert.Equal(136m, (await AnalyzeAsync(c, c.Org.Anna, new(2026, 10, 1), new(2026, 10, 31))).NormHours);
    }

    [Theory]
    [InlineData(14, false)]
    [InlineData(15, true)]
    public async Task UC14_Annual_overtime_limit_flag(int lastEntryHours, bool exceeded)
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        // 12 dni po 20 h (12 h nadgodzin) = 144 h, plus wpis kończący: 14 h = 6 h (razem 150), 15 h = 7 h (razem 151).
        var days = new[] { 7, 8, 9, 12, 13, 14, 15, 16, 19, 20, 21, 22, 23 };
        var entries = days.Take(12).Select(d => Entry(c.Org.Anna, T(1, d, 0), T(1, d, 20))).ToList();
        entries.Add(Entry(c.Org.Anna, T(1, days[12], 0), T(1, days[12], lastEntryHours)));
        await AddAsync(c, entries.ToArray());

        var analysis = await AnalyzeAsync(c, c.Org.Anna, new(2026, 1, 1), new(2026, 1, 31));

        Assert.Equal(exceeded ? 151m : 150m, analysis.OvertimeYearToDate);
        Assert.Equal(exceeded, analysis.AnnualOvertimeLimitExceeded);
    }

    [Fact]
    public async Task UC15_Manager_analyzes_team_member_but_not_other_team()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var ewa = c.Timesheet(c.As(c.Org.Ewa, Roles.Manager));

        Assert.True((await ewa.AnalyzeAsync(c.Org.Anna, new(2026, 10, 1), new(2026, 10, 31))).IsSuccess);
        Assert.Equal(ServiceErrorCode.NotFound, (await ewa.AnalyzeAsync(c.Org.Tomasz, new(2026, 10, 1), new(2026, 10, 31))).Error!.Code);
    }

    [Fact]
    public async Task UC15_Employee_analyzes_only_self()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var anna = c.Timesheet(c.As(c.Org.Anna, Roles.Employee));

        Assert.True((await anna.AnalyzeAsync(c.Org.Anna, new(2026, 10, 1), new(2026, 10, 31))).IsSuccess);
        Assert.Equal(ServiceErrorCode.NotFound, (await anna.AnalyzeAsync(c.Org.Jan, new(2026, 10, 1), new(2026, 10, 31))).Error!.Code);
    }

    [Fact]
    public async Task UC15_Reversed_range_is_validation_error()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var result = await c.Timesheet(c.As(c.Org.Piotr, Roles.HR)).AnalyzeAsync(c.Org.Anna, new(2026, 10, 31), new(2026, 10, 1));

        Assert.Equal(ServiceErrorCode.Validation, result.Error!.Code);
    }

    [Fact]
    public async Task UC15_Overlapping_entries_in_database_give_validation_error()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        await AddAsync(c, Entry(c.Org.Anna, T(10, 12, 8), T(10, 12, 16)), Entry(c.Org.Anna, T(10, 12, 12), T(10, 12, 20)));

        var result = await c.Timesheet(c.As(c.Org.Piotr, Roles.HR)).AnalyzeAsync(c.Org.Anna, new(2026, 10, 12), new(2026, 10, 18));

        Assert.Equal(ServiceErrorCode.Validation, result.Error!.Code);
    }
}
