namespace Hr.Rules.Leave;

/// <summary>Wymiar urlopu wypoczynkowego (art. 154 § 1 Kodeksu pracy). Okresy nauki (art. 154¹) poza zakresem.</summary>
public static class LeaveEntitlement
{
    public static int YearsOfService(DateOnly hireDate, DateOnly asOf)
    {
        int years = asOf.Year - hireDate.Year;
        if (asOf < hireDate.AddYears(years))
        {
            years--;
        }

        return Math.Max(years, 0);
    }

    public static int AnnualDays(int yearsOfService) => yearsOfService >= 10 ? 26 : 20;
}
