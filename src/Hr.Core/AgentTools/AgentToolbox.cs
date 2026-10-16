using System.Globalization;
using System.Text.Json;
using Hr.Core.Abstractions;
using Hr.Core.Auditing;
using Hr.Core.Persistence;
using Hr.Core.Services;
using Hr.Rules.Overtime;

namespace Hr.Core.AgentTools;

/// <summary>
/// Narzędzia agenta (5.4): każde woła serwis aplikacyjny w kontekście zalogowanego użytkownika, a liczby pochodzą z silnika reguł.
/// Parametr <c>employee_id</c> jest honorowany tylko dla kadr; dla pozostałych narzędzia działają na własnych danych (UC-24).
/// </summary>
public sealed class AgentToolbox(
    HrDbContext db,
    ICurrentUser user,
    IClock clock,
    LeaveService leave,
    TimesheetService timesheet,
    PayrollService payroll,
    CorrectionDraftService drafts,
    LabourCodeSearch labourCode,
    EmployeeDirectoryService directory,
    IAuditWriter audit) : IAgentToolbox
{
    private const int MaxPeriodDays = 400;
    private const int MaxEmployeeHits = 10;
    private const int MaxAuditedInputLength = 2000;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string[] LocalTimeFormats = { "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm" };

    private bool IsHrStaff => user.IsInRole(Roles.HR) || user.IsInRole(Roles.HRApprover);

    private sealed class InvalidInputException(string message) : Exception(message);

    public IReadOnlyList<ToolDefinition> DefinitionsForCurrentUser()
    {
        bool hr = IsHrStaff;
        var tools = new List<ToolDefinition>
        {
            new("get_leave_balance",
                "Zwraca saldo urlopu wypoczynkowego w bieżącym roku (wymiar, wykorzystane, oczekujące, pozostałe dni) obliczone przez silnik reguł. Nie licz dni samodzielnie.",
                Schema(hr)),
            new("calculate_overtime",
                "Zwraca wymiar czasu pracy, przepracowane godziny i nadgodziny (50% i 100%) w okresie oraz od początku roku, obliczone przez silnik reguł. Nie licz godzin samodzielnie.",
                Schema(hr, ("from", "string", "Początek okresu, format yyyy-MM-dd.", true), ("to", "string", "Koniec okresu włącznie, format yyyy-MM-dd.", true))),
            new("check_work_time_violations",
                "Sprawdza naruszenia norm czasu pracy w okresie: odpoczynek dobowy (11 h), tygodniowy (35 h) i średnią tygodniową (48 h). Zwraca listę naruszeń z identyfikatorem reguły.",
                Schema(hr, ("from", "string", "Początek okresu, format yyyy-MM-dd.", true), ("to", "string", "Koniec okresu włącznie, format yyyy-MM-dd.", true))),
            new("get_payroll_result",
                "Odczytuje zapisane wstępne naliczenie wynagrodzenia za miesiąc (podstawa, dodatki, brutto). Niczego nie oblicza ani nie uruchamia; gdy naliczenia nie wykonano, zwraca błąd i nie wolno podawać kwoty.",
                Schema(hr, ("year", "integer", "Rok, np. 2026.", true), ("month", "integer", "Miesiąc 1-12.", true))),
            new("list_work_entries",
                "Wypisuje wpisy czasu pracy w okresie (identyfikator, początek, koniec, przerwa). Identyfikator wpisu jest potrzebny do propose_correction.",
                Schema(hr, ("from", "string", "Początek okresu, format yyyy-MM-dd.", true), ("to", "string", "Koniec okresu włącznie, format yyyy-MM-dd.", true))),
            new("search_labour_code",
                "Wyszukuje przepisy Kodeksu pracy po numerze artykułu (np. 'art. 151[1]') albo słowach kluczowych. Zwraca tekst i cytat z numerem artykułu.",
                Schema(false, ("query", "string", "Numer artykułu albo słowa kluczowe.", true))),
        };

        if (hr)
        {
            tools.Add(new("find_employee",
                "Wyszukuje pracownika po imieniu lub nazwisku (do 10 wyników) i zwraca jego identyfikator do parametru employee_id innych narzędzi.",
                Schema(false, ("query", "string", "Imię, nazwisko lub ich część.", true))));
            tools.Add(new("propose_correction",
                "Przygotowuje szkic korekty wpisu czasu pracy dla działu kadr (status Pending). Identyfikator wpisu weź z list_work_entries. Nie zmienia ewidencji; zmianę zatwierdza człowiek.",
                Schema(false,
                    ("work_entry_id", "string", "Identyfikator wpisu czasu pracy (GUID).", true),
                    ("start", "string", "Proponowany początek, czas lokalny bez strefy, np. 2026-10-05T08:00:00.", true),
                    ("end", "string", "Proponowany koniec, czas lokalny bez strefy.", true),
                    ("break_minutes", "integer", "Przerwa w minutach.", true),
                    ("reason", "string", "Uzasadnienie korekty.", true))));
        }

        return tools;
    }

    public async Task<ToolOutcome> ExecuteAsync(string toolName, string inputJson, Guid? conversationId, CancellationToken ct)
    {
        ToolOutcome outcome;
        try
        {
            using var document = ParseInput(inputJson);
            var input = document.RootElement;
            outcome = toolName switch
            {
                "get_leave_balance" => await GetLeaveBalanceAsync(input, ct),
                "calculate_overtime" => await CalculateOvertimeAsync(input, ct),
                "check_work_time_violations" => await CheckViolationsAsync(input, ct),
                "get_payroll_result" => await GetPayrollAsync(input, ct),
                "list_work_entries" => await ListWorkEntriesAsync(input, ct),
                "find_employee" => await FindEmployeeAsync(input, ct),
                "search_labour_code" => await SearchLabourCodeAsync(input, ct),
                "propose_correction" => await ProposeCorrectionAsync(input, conversationId, ct),
                _ => Fail("UnknownTool", $"Nieznane narzędzie: {toolName}."),
            };
        }
        catch (InvalidInputException ex)
        {
            outcome = Fail("InvalidInput", ex.Message);
        }

        audit.Record(
            "AgentToolCall",
            "Conversation",
            conversationId?.ToString() ?? "none",
            JsonSerializer.Serialize(new { tool = toolName, parameters = Truncate(inputJson), ok = !outcome.IsError }, Json));
        await db.SaveChangesAsync(ct);
        return outcome;
    }

    private async Task<ToolOutcome> GetLeaveBalanceAsync(JsonElement input, CancellationToken ct)
    {
        if (TargetEmployee(input) is not { } employeeId)
        {
            return NotLinked();
        }

        var result = await leave.GetBalanceAsync(employeeId, ct);
        if (!result.IsSuccess)
        {
            return FromError(result.Error!);
        }

        var b = result.Value!;
        return Ok(new
        {
            employeeId,
            year = clock.LocalNow.Year,
            entitlementDays = b.EntitlementDays,
            approvedDays = b.ApprovedDays,
            pendingDays = b.PendingDays,
            remainingDays = b.RemainingDays,
            ruleId = "KP-154-1",
        });
    }

    private async Task<ToolOutcome> CalculateOvertimeAsync(JsonElement input, CancellationToken ct)
    {
        var (from, to) = ReadPeriod(input, out var tooLong);
        if (tooLong is not null)
        {
            return tooLong;
        }

        if (TargetEmployee(input) is not { } employeeId)
        {
            return NotLinked();
        }

        var result = await timesheet.AnalyzeAsync(employeeId, from, to, ct);
        if (!result.IsSuccess)
        {
            return FromError(result.Error!);
        }

        var a = result.Value!;
        return Ok(new
        {
            employeeId,
            from,
            to,
            normHours = a.NormHours,
            workedHours = a.WorkedHours,
            overtimeHours50 = a.Overtime.Hours50,
            overtimeHours100 = a.Overtime.Hours100,
            overtimeTotal = a.Overtime.Total,
            overtimeYearToDate = a.OvertimeYearToDate,
            annualOvertimeLimitHours = OvertimeLimit.AnnualHours,
            annualOvertimeLimitExceeded = a.AnnualOvertimeLimitExceeded,
            ruleIds = a.Overtime.RuleIds.Append(OvertimeLimit.RuleId).Distinct().ToList(),
        });
    }

    private async Task<ToolOutcome> CheckViolationsAsync(JsonElement input, CancellationToken ct)
    {
        var (from, to) = ReadPeriod(input, out var tooLong);
        if (tooLong is not null)
        {
            return tooLong;
        }

        if (TargetEmployee(input) is not { } employeeId)
        {
            return NotLinked();
        }

        var result = await timesheet.AnalyzeAsync(employeeId, from, to, ct);
        if (!result.IsSuccess)
        {
            return FromError(result.Error!);
        }

        var a = result.Value!;
        return Ok(new
        {
            employeeId,
            from,
            to,
            restViolationCount = a.RestViolations.Count,
            restViolations = a.RestViolations
                .Select(v => new { v.RuleId, v.From, v.To, v.RestHours, v.RequiredHours })
                .ToList(),
            weeklyAverage = new { a.WeeklyAverage.AverageWeeklyHours, a.WeeklyAverage.ExceedsLimit, a.WeeklyAverage.RuleId },
        });
    }

    private async Task<ToolOutcome> ListWorkEntriesAsync(JsonElement input, CancellationToken ct)
    {
        var (from, to) = ReadPeriod(input, out var tooLong);
        if (tooLong is not null)
        {
            return tooLong;
        }

        if (to < from)
        {
            return Fail("Validation", "Koniec okresu nie może być przed jego początkiem.");
        }

        if (TargetEmployee(input) is not { } employeeId)
        {
            return NotLinked();
        }

        if (!(await directory.ListAsync(ct)).Any(e => e.Id == employeeId))
        {
            return Fail("NotFound", "Nie znaleziono pracownika.");
        }

        var entries = (await timesheet.ListAsync(from, to, ct)).Where(w => w.EmployeeId == employeeId).ToList();
        return Ok(new
        {
            employeeId,
            from,
            to,
            count = entries.Count,
            entries = entries.Select(w => new { w.Id, w.Start, w.End, breakMinutes = (int)w.Break.TotalMinutes }).ToList(),
        });
    }

    private async Task<ToolOutcome> FindEmployeeAsync(JsonElement input, CancellationToken ct)
    {
        if (!IsHrStaff)
        {
            return Fail("Forbidden", "Wyszukiwanie pracowników jest dostępne dla działu kadr.");
        }

        string query = RequiredString(input, "query").Trim();
        var compare = CultureInfo.InvariantCulture.CompareInfo;
        const CompareOptions Loose = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var found = (await directory.ListAsync(ct))
            .Where(e => words.All(w => compare.IndexOf($"{e.FirstName} {e.LastName}", w, Loose) >= 0))
            .Take(MaxEmployeeHits)
            .Select(e => new { e.Id, e.FirstName, e.LastName, e.TeamName })
            .ToList();
        return Ok(new { query, count = found.Count, employees = found });
    }

    private async Task<ToolOutcome> GetPayrollAsync(JsonElement input, CancellationToken ct)
    {
        int year = RequiredInt(input, "year");
        int month = RequiredInt(input, "month");
        if (month is < 1 or > 12 || year is < 2000 or > 2100)
        {
            return Fail("Validation", "Nieprawidłowy rok lub miesiąc.");
        }

        if (TargetEmployee(input) is not { } employeeId)
        {
            return NotLinked();
        }

        var result = await payroll.GetAsync(employeeId, year, month, ct);
        if (!result.IsSuccess)
        {
            return result.Error!.Code == ServiceErrorCode.NotFound
                ? Fail("NotFound", $"Naliczenie za {month:00}/{year} nie zostało jeszcze wykonane przez dział kadr. Nie podawaj kwoty ani jej nie szacuj.")
                : FromError(result.Error);
        }

        var r = result.Value!;
        return Ok(new
        {
            employeeId,
            year,
            month,
            baseSalary = r.BaseSalary,
            fixedAllowance = r.FixedAllowance,
            normHours = r.NormHours,
            overtime50Hours = r.Overtime50Hours,
            overtime100Hours = r.Overtime100Hours,
            normalOvertimePay = r.NormalOvertimePay,
            allowance50 = r.Allowance50,
            allowance100 = r.Allowance100,
            gross = r.Gross,
            ruleIds = r.RuleIds.Split(',', StringSplitOptions.RemoveEmptyEntries),
        });
    }

    private async Task<ToolOutcome> SearchLabourCodeAsync(JsonElement input, CancellationToken ct)
    {
        string query = RequiredString(input, "query");
        var hits = await labourCode.SearchAsync(query, 5, ct);
        return Ok(new
        {
            query,
            count = hits.Count,
            results = hits.Select(h => new { h.Number, h.Citation, h.Text }).ToList(),
            message = hits.Count == 0 ? "Nie znaleziono przepisów dla tego zapytania; nie cytuj z pamięci." : null,
        });
    }

    private async Task<ToolOutcome> ProposeCorrectionAsync(JsonElement input, Guid? conversationId, CancellationToken ct)
    {
        if (!IsHrStaff)
        {
            return Fail("Forbidden", "Szkice korekt przygotowuje dział kadr.");
        }

        var entryId = RequiredGuid(input, "work_entry_id");
        var start = RequiredLocalTime(input, "start");
        var end = RequiredLocalTime(input, "end");
        int breakMinutes = RequiredInt(input, "break_minutes");
        string reason = RequiredString(input, "reason");
        if (breakMinutes < 0)
        {
            return Fail("Validation", "Przerwa nie może być ujemna.");
        }

        var result = await drafts.CreateAsync(entryId, start, end, TimeSpan.FromMinutes(breakMinutes), reason, conversationId, ct);
        return result.IsSuccess
            ? Ok(new { draftId = result.Value!.Id, status = result.Value.Status.ToString(), employeeId = result.Value.EmployeeId, workEntryId = result.Value.WorkEntryId })
            : FromError(result.Error!);
    }

    /// <summary>Kadry mogą wskazać pracownika; dla pozostałych <c>employee_id</c> jest ignorowany, nawet gdy model go poda (UC-24).</summary>
    private Guid? TargetEmployee(JsonElement input)
    {
        if (IsHrStaff && input.TryGetProperty("employee_id", out var value) && value.ValueKind != JsonValueKind.Null)
        {
            return RequiredGuid(input, "employee_id");
        }

        return user.EmployeeId;
    }

    private static (DateOnly From, DateOnly To) ReadPeriod(JsonElement input, out ToolOutcome? tooLong)
    {
        var from = RequiredDate(input, "from");
        var to = RequiredDate(input, "to");
        tooLong = to.DayNumber - from.DayNumber > MaxPeriodDays ? Fail("Validation", $"Okres nie może przekraczać {MaxPeriodDays} dni.") : null;
        return (from, to);
    }

    private static JsonDocument ParseInput(string inputJson)
    {
        try
        {
            var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(inputJson) ? "{}" : inputJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                document.Dispose();
                throw new InvalidInputException("Parametry muszą być obiektem JSON.");
            }

            return document;
        }
        catch (JsonException)
        {
            throw new InvalidInputException("Parametry nie są poprawnym JSON.");
        }
    }

    private static JsonElement Required(JsonElement input, string name) =>
        input.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value
            : throw new InvalidInputException($"Brak wymaganego pola „{name}”.");

    private static string RequiredString(JsonElement input, string name)
    {
        var value = Required(input, name);
        return value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidInputException($"Pole „{name}” musi być niepustym tekstem.");
    }

    private static int RequiredInt(JsonElement input, string name) =>
        Required(input, name) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt32(out int number)
            ? number
            : throw new InvalidInputException($"Pole „{name}” musi być liczbą całkowitą.");

    private static Guid RequiredGuid(JsonElement input, string name) =>
        Guid.TryParse(RequiredString(input, name), out var id) ? id : throw new InvalidInputException($"Pole „{name}” musi być identyfikatorem GUID.");

    private static DateOnly RequiredDate(JsonElement input, string name) =>
        DateOnly.TryParseExact(RequiredString(input, name), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new InvalidInputException($"Pole „{name}” musi mieć format yyyy-MM-dd.");

    private static DateTime RequiredLocalTime(JsonElement input, string name) =>
        DateTime.TryParseExact(RequiredString(input, name), LocalTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time
            : throw new InvalidInputException($"Pole „{name}” musi być czasem lokalnym bez strefy, np. 2026-10-05T08:00:00.");

    private static string Truncate(string value) => value.Length <= MaxAuditedInputLength ? value : value[..MaxAuditedInputLength];

    private static ToolOutcome Ok(object value) => new(JsonSerializer.Serialize(value, Json), false);

    private static ToolOutcome Fail(string code, string message, string? ruleId = null) =>
        new(JsonSerializer.Serialize(new { error = new { code, message, ruleId } }, Json), true);

    private static ToolOutcome FromError(ServiceError error) => Fail(error.Code.ToString(), error.Message, error.RuleId);

    private static ToolOutcome NotLinked() => Fail("Forbidden", "Użytkownik nie jest powiązany z pracownikiem.");

    private static string Schema(bool withEmployeeId, params (string Name, string Type, string Description, bool Required)[] properties)
    {
        var props = properties.ToDictionary(p => p.Name, p => (object)new { type = p.Type, description = p.Description });
        if (withEmployeeId)
        {
            props["employee_id"] = new { type = "string", description = "Identyfikator pracownika (GUID, znajdź go narzędziem find_employee); pomiń, aby pytać o siebie." };
        }

        return JsonSerializer.Serialize(
            new { type = "object", properties = props, required = properties.Where(p => p.Required).Select(p => p.Name).ToArray() }, Json);
    }
}
