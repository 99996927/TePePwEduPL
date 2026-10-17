using Hr.Core.Abstractions;
using Hr.Core.Domain;

namespace Hr.Core.Seeding.Demo;

/// <summary>
/// Zmiany płacowe demo (UC-18, UC-20): zatwierdzona zmiana Jana (od 1. dnia miesiąca M-4), oczekująca zmiana Anny
/// (od 1. dnia miesiąca M+1) i odrzucona zmiana Tomasza (od 1. dnia miesiąca M-1), gdzie M to miesiąc kotwicy.
/// Wszystkie tworzy Piotr, a decyduje Maria (zasada czterech oczu).
/// </summary>
public static class DemoPayrollChanges
{
    public static IReadOnlyList<PayrollChange> Build(DemoOptions o, IReadOnlyList<DemoPerson> people)
    {
        var piotr = people.Single(p => p.Login == "piotr.zielinski");
        var maria = people.Single(p => p.Login == "maria.kaminska");
        var jan = people.Single(p => p.Login == "jan.kowalski");
        var anna = people.Single(p => p.Login == "anna.nowak");
        var tomasz = people.Single(p => p.Login == "tomasz.lis");
        var month = new DateOnly(o.AnchorDate.Year, o.AnchorDate.Month, 1);

        return new List<PayrollChange>
        {
            Create(o, jan, PayrollComponent.FixedAllowance, 400m, month.AddMonths(-4),
                "Dodatek stały za rozszerzony zakres obowiązków.",
                PayrollChangeStatus.Approved, piotr, maria, "Zatwierdzono zgodnie z wnioskiem kierownika.",
                month.AddMonths(-4).AddDays(-14)),
            Create(o, anna, PayrollComponent.BaseSalary, 7800m, month.AddMonths(1),
                "Podwyżka po rocznej ocenie pracy.",
                PayrollChangeStatus.Pending, piotr, null, null,
                o.AnchorDate.AddDays(-3)),
            Create(o, tomasz, PayrollComponent.FixedAllowance, 300m, month.AddMonths(-1),
                "Dodatek za pracę w zmiennych godzinach.",
                PayrollChangeStatus.Rejected, piotr, maria, "Brak podstaw do przyznania dodatku w obecnym budżecie.",
                month.AddMonths(-1).AddDays(-14)),
        };
    }

    private static PayrollChange Create(
        DemoOptions o, DemoPerson employee, PayrollComponent component, decimal value, DateOnly effectiveFrom,
        string reason, PayrollChangeStatus status, DemoPerson creator, DemoPerson? decider, string? decisionReason,
        DateOnly createdOn) =>
        new()
        {
            Id = DemoIds.For(o.Seed, $"payroll-change:{employee.Login}"),
            EmployeeId = employee.EmployeeId,
            Component = component,
            Value = value,
            EffectiveFrom = effectiveFrom,
            Reason = reason,
            Status = status,
            CreatedByUserId = creator.UserId,
            DecidedByUserId = decider?.UserId,
            DecisionReason = decisionReason,
            CreatedAtUtc = PolishTime.ToUtc(createdOn.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Unspecified)),
        };
}
