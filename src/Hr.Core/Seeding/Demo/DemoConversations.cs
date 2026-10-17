using System.Text.Json;
using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Rules.Leave;

namespace Hr.Core.Seeding.Demo;

/// <summary>
/// Rozmowy z agentem demo: Anna pyta o saldo urlopu (UC-21, UC-30), Tomasz pyta o urlop Anny i dostaje odmowę (UC-24).
/// Znaczniki czasu: kotwica 9:00 czasu lokalnego, każda kolejna wiadomość minutę później.
/// </summary>
public static class DemoConversations
{
    private const string AnnaToolUseId = "toolu_demo_anna_01";
    private const string LeaveBalanceTool = "get_leave_balance";

    // Te same opcje co w AgentToolbox, żeby JSON wyniku narzędzia miał identyczny kształt.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<Conversation> Build(DemoOptions o, IReadOnlyList<DemoPerson> people, IReadOnlyList<LeaveRequest> leave)
    {
        var anna = people.Single(p => p.Login == "anna.nowak");
        var tomasz = people.Single(p => p.Login == "tomasz.lis");
        return new List<Conversation> { AnnaConversation(o, anna, leave), TomaszConversation(o, tomasz) };
    }

    private static Conversation AnnaConversation(DemoOptions o, DemoPerson anna, IReadOnlyList<LeaveRequest> leave)
    {
        var year = o.AnchorDate.Year;
        var mine = leave.Where(l => l.EmployeeId == anna.EmployeeId && l.From.Year == year).ToList();
        var entitlement = LeaveEntitlement.AnnualDays(LeaveEntitlement.YearsOfService(anna.HireDate, o.AnchorDate));
        var approved = mine.Where(l => l.Status == LeaveStatus.Approved).Sum(l => l.Days);
        var pending = mine.Where(l => l.Status == LeaveStatus.Pending).Sum(l => l.Days);
        var remaining = entitlement - approved - pending;

        var result = JsonSerializer.Serialize(new
        {
            employeeId = anna.EmployeeId,
            year,
            entitlementDays = entitlement,
            approvedDays = approved,
            pendingDays = pending,
            remainingDays = remaining,
            ruleId = "KP-154-1",
        }, Json);

        var answer = $"W tym roku przysługuje Ci {entitlement} dni urlopu: {approved} dni zatwierdzonych, " +
                     $"{pending} dni oczekuje na decyzję, zostało {remaining} dni.";

        return Create(o, anna, "anna.nowak", new[]
        {
            (MessageRole.User, "Ile dni urlopu mi zostało?", null, null),
            (MessageRole.ToolCall, AnnaToolUseId, LeaveBalanceTool, "{}"),
            (MessageRole.ToolResult, result, LeaveBalanceTool, null),
            (MessageRole.Assistant, answer, null, null),
        });
    }

    private static Conversation TomaszConversation(DemoOptions o, DemoPerson tomasz) =>
        Create(o, tomasz, "tomasz.lis", new (MessageRole, string, string?, string?)[]
        {
            (MessageRole.User, "Czy Anna Nowak ma zaplanowany urlop w grudniu?", null, null),
            (MessageRole.Assistant,
                "Przepraszam, nie mogę udostępniać danych urlopowych innych osób. " +
                "Mogę za to sprawdzić Twoje własne dane, na przykład Twoje saldo urlopu lub wnioski.", null, null),
        });

    private static Conversation Create(
        DemoOptions o, DemoPerson owner, string key, IReadOnlyList<(MessageRole Role, string Content, string? ToolName, string? ToolParams)> items)
    {
        var started = PolishTime.ToUtc(o.AnchorDate.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Unspecified));
        var id = DemoIds.For(o.Seed, $"conversation:{key}");
        return new Conversation
        {
            Id = id,
            UserId = owner.UserId,
            StartedAtUtc = started,
            Messages = items.Select((m, i) => new Message
            {
                Id = DemoIds.For(o.Seed, $"conversation:{key}:message:{i + 1}"),
                ConversationId = id,
                Seq = i + 1,
                Role = m.Role,
                Content = m.Content,
                ToolName = m.ToolName,
                ToolParamsJson = m.ToolParams,
                CreatedAtUtc = started.AddMinutes(i),
            }).ToList(),
        };
    }
}
