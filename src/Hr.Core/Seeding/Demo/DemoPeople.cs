using System.Text;
using Hr.Core.Abstractions;

namespace Hr.Core.Seeding.Demo;

/// <summary>Zespoły i osoby demo: sześć kont stałych oraz osoby generowane z ziarna.</summary>
public static class DemoPeople
{
    public const string TeamEwa = "Zespół Ewy";
    public const string TeamB = "Zespół B";
    public const string TeamC = "Zespół C";
    public const string TeamD = "Zespół D";
    public const string TeamHr = "Kadry";

    private sealed record Named(string First, string Last, string Role, string Team, DateOnly Hired, decimal Salary);

    private static readonly Named[] NamedPeople =
    {
        new("Anna", "Nowak", Roles.Employee, TeamEwa, new DateOnly(2019, 3, 1), 7200m),
        new("Jan", "Kowalski", Roles.Employee, TeamEwa, new DateOnly(2014, 5, 1), 8000m),
        new("Tomasz", "Lis", Roles.Employee, TeamB, new DateOnly(2022, 9, 1), 6800m),
        new("Ewa", "Wiśniewska", Roles.Manager, TeamEwa, new DateOnly(2012, 1, 2), 11000m),
        new("Piotr", "Zieliński", Roles.HR, TeamHr, new DateOnly(2018, 6, 1), 9000m),
        new("Maria", "Kamińska", Roles.HRApprover, TeamHr, new DateOnly(2015, 2, 1), 12000m),
    };

    // Miejsca do uzupełnienia osobami generowanymi, w stałej kolejności.
    private static readonly (string Team, string Role)[] GeneratedSlots =
        Enumerable.Repeat((TeamEwa, Roles.Employee), 4)
            .Append((TeamB, Roles.Manager)).Concat(Enumerable.Repeat((TeamB, Roles.Employee), 5))
            .Append((TeamC, Roles.Manager)).Concat(Enumerable.Repeat((TeamC, Roles.Employee), 6))
            .Append((TeamD, Roles.Manager)).Concat(Enumerable.Repeat((TeamD, Roles.Employee), 6))
            .Append((TeamHr, Roles.Employee))
            .ToArray();

    private static readonly string[] FemaleFirstNames =
    {
        "Agnieszka", "Barbara", "Celina", "Dorota", "Elżbieta", "Felicja", "Grażyna", "Halina", "Irena", "Joanna",
        "Katarzyna", "Lidia", "Małgorzata", "Natalia", "Olga", "Paulina", "Renata", "Sylwia", "Teresa", "Urszula",
        "Wiktoria", "Zofia", "Bożena", "Ewelina", "Żaneta", "Łucja", "Magdalena", "Karolina", "Iwona", "Aleksandra",
    };

    private static readonly string[] MaleFirstNames =
    {
        "Adam", "Bartosz", "Cezary", "Dariusz", "Emil", "Filip", "Grzegorz", "Henryk", "Igor", "Józef",
        "Krzysztof", "Łukasz", "Marek", "Norbert", "Oskar", "Paweł", "Robert", "Sebastian", "Tadeusz", "Waldemar",
        "Zbigniew", "Błażej", "Radosław", "Michał", "Jędrzej", "Mateusz", "Konrad", "Damian", "Rafał", "Wojciech",
    };

    // (męskie, żeńskie)
    private static readonly (string Male, string Female)[] LastNames =
    {
        ("Żółkiewski", "Żółkiewska"), ("Wójcik", "Wójcik"), ("Kowalczyk", "Kowalczyk"), ("Woźniak", "Woźniak"),
        ("Mazur", "Mazur"), ("Krawczyk", "Krawczyk"), ("Kaczmarek", "Kaczmarek"), ("Piotrowski", "Piotrowska"),
        ("Grabowski", "Grabowska"), ("Pawłowski", "Pawłowska"), ("Michalski", "Michalska"), ("Król", "Król"),
        ("Wieczorek", "Wieczorek"), ("Jabłoński", "Jabłońska"), ("Wróbel", "Wróbel"), ("Dudek", "Dudek"),
        ("Adamczyk", "Adamczyk"), ("Majewski", "Majewska"), ("Olszewski", "Olszewska"), ("Stępień", "Stępień"),
        ("Jaworski", "Jaworska"), ("Malinowski", "Malinowska"), ("Górski", "Górska"), ("Pawlak", "Pawlak"),
        ("Sikora", "Sikora"), ("Walczak", "Walczak"), ("Baran", "Baran"), ("Rutkowski", "Rutkowska"),
        ("Michalak", "Michalak"), ("Szewczyk", "Szewczyk"), ("Ostrowski", "Ostrowska"), ("Tomaszewski", "Tomaszewska"),
        ("Pietrzak", "Pietrzak"), ("Marciniak", "Marciniak"), ("Zalewski", "Zalewska"), ("Jakubowski", "Jakubowska"),
        ("Sadowski", "Sadowska"), ("Duda", "Duda"), ("Włodarczyk", "Włodarczyk"), ("Chmielewski", "Chmielewska"),
        ("Borkowski", "Borkowska"), ("Czarnecki", "Czarnecka"), ("Sawicki", "Sawicka"), ("Sokołowski", "Sokołowska"),
    };

    private const decimal BaseSalary = 5000m;
    private const decimal PerYearSalary = 300m;
    private const decimal ManagerFactor = 1.25m;
    private const decimal Step = 50m;
    private static readonly DateOnly EarliestHire = new(2008, 1, 1);

    public static (IReadOnlyList<DemoTeam>, IReadOnlyList<DemoPerson>) Build(DemoOptions o)
    {
        var people = new List<DemoPerson>();
        foreach (var n in NamedPeople)
        {
            people.Add(Person(o, n.First, n.Last, n.Role, n.Team, n.Hired, n.Salary, isNamed: true));
        }

        var teamNames = o.Profile == DemoProfile.Accounts
            ? new[] { TeamEwa, TeamB, TeamHr }
            : new[] { TeamEwa, TeamB, TeamC, TeamD, TeamHr };

        if (o.Profile == DemoProfile.Full)
        {
            people.AddRange(Generate(o));
        }

        var teams = teamNames
            .Select(name => new DemoTeam(
                DemoIds.For(o.Seed, "team:" + name),
                name,
                people.FirstOrDefault(p => p.TeamName == name && p.Role == Roles.Manager)?.EmployeeId))
            .ToList();
        return (teams, people);
    }

    /// <summary>Zakres wynagrodzenia wygenerowanej osoby: 5000 + 300 * min(lata, 10) ±5%, Manager ×1,25, w krokach po 50 zł.</summary>
    public static (decimal Min, decimal Max) SalaryRange(int yearsOfService, string role)
    {
        var baseValue = BaseSalary + PerYearSalary * Math.Min(Math.Max(yearsOfService, 0), 10);
        if (role == Roles.Manager)
        {
            baseValue *= ManagerFactor;
        }

        var min = Math.Ceiling(baseValue * 0.95m / Step) * Step;
        var max = Math.Floor(baseValue * 1.05m / Step) * Step;
        return (min, max);
    }

    private static IEnumerable<DemoPerson> Generate(DemoOptions o)
    {
        var rnd = new Random(o.Seed);
        var females = Shuffle(rnd, FemaleFirstNames);
        var males = Shuffle(rnd, MaleFirstNames);
        var reserved = NamedPeople.Select(n => Ascii(n.Last)).ToHashSet();
        var lastNames = Shuffle(rnd, LastNames.Where(l => !reserved.Contains(Ascii(l.Male))).ToArray());
        var hireDays = o.WindowStart.DayNumber - EarliestHire.DayNumber; // dni od 2008-01-01 do WindowStart - 1

        int f = 0, m = 0;
        for (var i = 0; i < GeneratedSlots.Length; i++)
        {
            var (team, role) = GeneratedSlots[i];
            var female = rnd.Next(2) == 0;
            var first = female ? females[f++] : males[m++];
            var last = female ? lastNames[i].Female : lastNames[i].Male;
            var hired = EarliestHire.AddDays(rnd.Next(hireDays));
            var (min, max) = SalaryRange(YearsOfService(hired, o.AnchorDate), role);
            var factor = 0.95m + (decimal)rnd.NextDouble() * 0.10m;
            var raw = (BaseSalary + PerYearSalary * Math.Min(YearsOfService(hired, o.AnchorDate), 10))
                * (role == Roles.Manager ? ManagerFactor : 1m) * factor;
            var salary = Math.Clamp(Math.Round(raw / Step, MidpointRounding.AwayFromZero) * Step, min, max);
            yield return Person(o, first, last, role, team, hired, salary, isNamed: false);
        }
    }

    private static DemoPerson Person(
        DemoOptions o, string first, string last, string role, string team, DateOnly hired, decimal salary, bool isNamed)
    {
        var login = $"{Ascii(first)}.{Ascii(last)}";
        return new DemoPerson(first, last, login, role, team, hired, salary,
            DemoIds.For(o.Seed, "employee:" + login), DemoIds.For(o.Seed, "user:" + login), isNamed);
    }

    private static int YearsOfService(DateOnly hired, DateOnly asOf)
    {
        var years = asOf.Year - hired.Year;
        return asOf < hired.AddYears(years) ? years - 1 : years;
    }

    private static T[] Shuffle<T>(Random rnd, T[] source)
    {
        var a = (T[])source.Clone();
        for (var i = a.Length - 1; i > 0; i--)
        {
            var j = rnd.Next(i + 1);
            (a[i], a[j]) = (a[j], a[i]);
        }

        return a;
    }

    private static string Ascii(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s.ToLowerInvariant())
        {
            sb.Append(c switch
            {
                'ą' => 'a', 'ć' => 'c', 'ę' => 'e', 'ł' => 'l', 'ń' => 'n',
                'ó' => 'o', 'ś' => 's', 'ź' => 'z', 'ż' => 'z', _ => c,
            });
        }

        return sb.ToString();
    }
}
