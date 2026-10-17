using System.Text.Json;
using System.Text.RegularExpressions;
using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Hr.Core.Seeding.Demo;
using Hr.Rules.Calendar;

namespace Hr.Core.Tests.Seeding;

public class DemoConversationsTests
{
    private static readonly DemoOptions Options = DemoOptions.Default;
    private static readonly IReadOnlyList<DemoPerson> People = DemoPeople.Build(Options).Item2;
    private static readonly IReadOnlyList<LeaveRequest> Leave = DemoLeave.Build(Options, People, new WorkCalendar());

    private static DemoPerson Person(string login) => People.Single(p => p.Login == login);

    private static IReadOnlyList<Conversation> Build() => DemoConversations.Build(Options, People, Leave);

    private static Conversation ConversationOf(string login) =>
        Build().Single(c => c.UserId == Person(login).UserId);

    [Fact]
    public void UC30_Anna_conversation_has_user_toolcall_toolresult_assistant_in_order()
    {
        var messages = ConversationOf("anna.nowak").Messages;

        Assert.Equal(
            new[] { MessageRole.User, MessageRole.ToolCall, MessageRole.ToolResult, MessageRole.Assistant },
            messages.Select(m => m.Role));
        Assert.Equal(new[] { 1, 2, 3, 4 }, messages.Select(m => m.Seq));
        Assert.Equal("get_leave_balance", messages[1].ToolName);
        Assert.Equal("toolu_demo_anna_01", messages[1].Content);
        Assert.Equal("{}", messages[1].ToolParamsJson);
        Assert.Equal("get_leave_balance", messages[2].ToolName);
    }

    [Fact]
    public void UC21_Annas_tool_result_matches_leave_balance_from_dataset()
    {
        var anna = Person("anna.nowak");
        var messages = ConversationOf("anna.nowak").Messages;
        using var json = JsonDocument.Parse(messages[2].Content);
        var root = json.RootElement;

        var mine = Leave.Where(l => l.EmployeeId == anna.EmployeeId && l.From.Year == Options.AnchorDate.Year).ToList();
        var approved = mine.Where(l => l.Status == LeaveStatus.Approved).Sum(l => l.Days);
        var pending = mine.Where(l => l.Status == LeaveStatus.Pending).Sum(l => l.Days);

        Assert.Equal(anna.EmployeeId, root.GetProperty("employeeId").GetGuid());
        Assert.Equal(Options.AnchorDate.Year, root.GetProperty("year").GetInt32());
        Assert.Equal(20, root.GetProperty("entitlementDays").GetInt32());
        Assert.Equal(10, root.GetProperty("approvedDays").GetInt32());
        Assert.Equal(5, root.GetProperty("pendingDays").GetInt32());
        Assert.Equal(5, root.GetProperty("remainingDays").GetInt32());
        Assert.Equal("KP-154-1", root.GetProperty("ruleId").GetString());
        Assert.Equal(approved, root.GetProperty("approvedDays").GetInt32());
        Assert.Equal(pending, root.GetProperty("pendingDays").GetInt32());

        var numbers = Regex.Matches(messages[3].Content, @"\d+").Select(m => m.Value).ToList();
        Assert.Equal(new[] { "20", "10", "5", "5" }, numbers);
    }

    [Fact]
    public void UC24_Tomasz_conversation_asks_about_Annas_leave_and_gets_refusal_without_tool_calls()
    {
        var messages = ConversationOf("tomasz.lis").Messages;

        Assert.Equal(new[] { MessageRole.User, MessageRole.Assistant }, messages.Select(m => m.Role));
        Assert.Equal(new[] { 1, 2 }, messages.Select(m => m.Seq));
        Assert.Contains("Ann", messages[0].Content);
        Assert.Contains("nie mogę", messages[1].Content);
        Assert.Contains("własn", messages[1].Content);
        Assert.All(messages, m => Assert.Null(m.ToolName));
    }

    [Fact]
    public void Conversations_are_deterministic_and_owned_by_their_users()
    {
        var conversations = Build();

        Assert.Equal(2, conversations.Count);
        Assert.All(conversations, c =>
        {
            Assert.All(c.Messages, m => Assert.Equal(c.Id, m.ConversationId));
            Assert.Equal(9, PolishTime.ToLocal(c.StartedAtUtc).Hour);
            Assert.Equal(Options.AnchorDate, DateOnly.FromDateTime(PolishTime.ToLocal(c.StartedAtUtc)));
            Assert.Equal(c.StartedAtUtc, c.Messages[0].CreatedAtUtc);
            for (var i = 1; i < c.Messages.Count; i++)
            {
                Assert.Equal(c.StartedAtUtc.AddMinutes(i), c.Messages[i].CreatedAtUtc);
            }
        });
        Assert.Equal(
            conversations.SelectMany(c => c.Messages).Select(m => m.Id).Distinct().Count(),
            conversations.Sum(c => c.Messages.Count));

        var options = new JsonSerializerOptions { ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles };
        var first = JsonSerializer.Serialize(conversations, options);
        var second = JsonSerializer.Serialize(DemoConversations.Build(Options, People, Leave), options);
        Assert.Contains("toolu_demo_anna_01", first);
        Assert.Equal(first, second);
    }
}
