using Hr.Core.Domain;
using Hr.Core.Services;
using Hr.Rules.Overtime;
using Hr.Rules.Rest;

namespace Hr.Api.Dtos;

public sealed record WorkEntryDto(Guid Id, Guid EmployeeId, DateTime Start, DateTime End, int BreakMinutes)
{
    public static WorkEntryDto Of(WorkEntryRecord w) => new(w.Id, w.EmployeeId, w.Start, w.End, (int)w.Break.TotalMinutes);
}

/// <summary>Czas lokalny bez strefy (ADR-17), np. <c>2026-10-12T08:00:00</c>.</summary>
public sealed record AddEntryRequest(DateTime Start, DateTime End, int BreakMinutes);

public sealed record CorrectEntryRequest(DateTime Start, DateTime End, int BreakMinutes, string Reason);

public sealed record OvertimeDto(decimal Hours50, decimal Hours100, decimal Total, IReadOnlyList<string> RuleIds)
{
    public static OvertimeDto Of(OvertimeResult o) => new(o.Hours50, o.Hours100, o.Total, o.RuleIds);
}

public sealed record RestViolationDto(string RuleId, DateTime From, DateTime To, decimal RestHours, decimal RequiredHours)
{
    public static RestViolationDto Of(RestViolation v) => new(v.RuleId, v.From, v.To, v.RestHours, v.RequiredHours);
}

public sealed record WeeklyAverageDto(decimal AverageWeeklyHours, bool ExceedsLimit, string RuleId)
{
    public static WeeklyAverageDto Of(AverageHoursResult a) => new(a.AverageWeeklyHours, a.ExceedsLimit, a.RuleId);
}

public sealed record TimesheetAnalysisDto(
    decimal NormHours,
    decimal WorkedHours,
    OvertimeDto Overtime,
    IReadOnlyList<RestViolationDto> RestViolations,
    WeeklyAverageDto WeeklyAverage,
    decimal OvertimeYearToDate,
    bool AnnualOvertimeLimitExceeded)
{
    public static TimesheetAnalysisDto Of(TimesheetAnalysis a) => new(
        a.NormHours,
        a.WorkedHours,
        OvertimeDto.Of(a.Overtime),
        a.RestViolations.Select(RestViolationDto.Of).ToList(),
        WeeklyAverageDto.Of(a.WeeklyAverage),
        a.OvertimeYearToDate,
        a.AnnualOvertimeLimitExceeded);
}
