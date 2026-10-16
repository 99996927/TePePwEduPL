namespace Hr.Rules.Payroll;

public sealed record PayrollInput(decimal MonthlyBaseSalary, decimal NormHours, decimal Overtime50Hours, decimal Overtime100Hours);

public sealed record PayrollResult(
    decimal BaseSalary,
    decimal HourlyRate,
    decimal Allowance50,
    decimal Allowance100,
    decimal Gross,
    IReadOnlyList<string> RuleIds)
{
    public bool Equals(PayrollResult? other) =>
        other is not null
        && BaseSalary == other.BaseSalary
        && HourlyRate == other.HourlyRate
        && Allowance50 == other.Allowance50
        && Allowance100 == other.Allowance100
        && Gross == other.Gross
        && RuleIds.SequenceEqual(other.RuleIds);

    public override int GetHashCode() => HashCode.Combine(BaseSalary, HourlyRate, Allowance50, Allowance100, Gross);
}

/// <summary>
/// Wstępne naliczenie brutto (ADR-22): podstawą dodatku jest wynagrodzenie zasadnicze (uproszczenie art. 151¹ § 3),
/// bez podatków i składek. Kwoty zaokrąglane do 2 miejsc (AwayFromZero).
/// </summary>
public static class PayrollCalculator
{
    public static PayrollResult Calculate(PayrollInput input)
    {
        if (input.MonthlyBaseSalary < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Wynagrodzenie nie może być ujemne.");
        }

        if (input.NormHours <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Wymiar czasu pracy musi być dodatni.");
        }

        if (input.Overtime50Hours < 0m || input.Overtime100Hours < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Godziny nadliczbowe nie mogą być ujemne.");
        }

        decimal rate = input.MonthlyBaseSalary / input.NormHours;
        decimal allowance50 = Round(input.Overtime50Hours * rate * 0.5m);
        decimal allowance100 = Round(input.Overtime100Hours * rate);
        decimal gross = input.MonthlyBaseSalary + allowance50 + allowance100;
        var ruleIds = input.Overtime50Hours + input.Overtime100Hours > 0m
            ? new[] { "KP-151-1-1" }
            : Array.Empty<string>();

        return new PayrollResult(input.MonthlyBaseSalary, Round(rate), allowance50, allowance100, gross, ruleIds);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
