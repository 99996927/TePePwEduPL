namespace Hr.Rules.Overtime;

/// <summary>Roczny limit godzin nadliczbowych z art. 151 § 3 Kodeksu pracy (reguła KP-151-3).</summary>
public static class OvertimeLimit
{
    public const string RuleId = "KP-151-3";

    public const decimal AnnualHours = 150m;

    public static bool Exceeded(decimal overtimeHoursInYear) => overtimeHoursInYear > AnnualHours;
}
