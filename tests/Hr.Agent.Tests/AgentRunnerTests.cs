using Hr.Agent;
using Hr.Core.AgentTools;
using Hr.TestSupport;
using Microsoft.Extensions.AI;

namespace Hr.Agent.Tests;

public class AgentRunnerTests
{
    private readonly ScriptedChatClient _model = new();
    private readonly FakeToolbox _toolbox = new();
    private readonly FakeConversationStore _store = new();

    private AgentRunner Runner() => new(AgentPipeline.Build(_model), _toolbox, _store);

    private Guid OnlyConversation() => _store.ConversationIds.Single();

    private IEnumerable<StoredRole> Roles(Guid id) => _store.Messages(id).Select(m => m.Role);

    [Fact]
    public async Task UC22_Runner_executes_tool_then_returns_final_text_and_stores_all_messages()
    {
        _toolbox.Results["calculate_overtime"] = new ToolOutcome("{\"overtimeTotal\":4}", false);
        _model.ThenToolCall("call_1", "calculate_overtime", new Dictionary<string, object?> { ["from"] = "2026-10-01", ["to"] = "2026-10-31" })
              .ThenText("Masz 4 godziny nadgodzin.");

        var result = await Runner().AskAsync(null, "Ile mam nadgodzin w październiku?", default);

        Assert.Null(result.Failure);
        Assert.Equal("Masz 4 godziny nadgodzin.", result.Reply!.Text);
        Assert.Empty(result.Reply.UngroundedNumbers);
        var stored = _store.Messages(OnlyConversation());
        Assert.Equal(new[] { StoredRole.User, StoredRole.ToolCall, StoredRole.ToolResult, StoredRole.Assistant }, stored.Select(m => m.Role));
        Assert.Equal("call_1", stored[1].Content);
        Assert.Equal("calculate_overtime", stored[1].ToolName);
        Assert.Contains("2026-10-01", stored[1].ToolParamsJson);
        Assert.Equal("{\"overtimeTotal\":4}", stored[2].Content);
        Assert.Equal(stored.Select(m => m.Seq), result.Reply.NewMessages.Select(m => m.Seq));
        var secondRound = _model.Requests[1].SelectMany(m => m.Contents).OfType<FunctionResultContent>().Single();
        Assert.Equal("call_1", secondRound.CallId);
        Assert.NotNull(_model.Options[0]?.Instructions);
        Assert.Equal(_toolbox.Definitions.Count, _model.Options[0]!.Tools!.Count);
    }

    [Fact]
    public async Task UC30_History_is_rebuilt_into_model_context_on_next_question()
    {
        _toolbox.Results["get_leave_balance"] = new ToolOutcome("{\"remainingDays\":7}", false);
        _toolbox.Results["calculate_overtime"] = new ToolOutcome("{\"overtimeTotal\":4}", false);
        _model.ThenToolCall("call_1", "get_leave_balance").ThenToolCall("call_2", "calculate_overtime").ThenText("Masz 7 dni i 4 godziny.")
              .ThenText("Tak, 7 dni.");
        var runner = Runner();
        await runner.AskAsync(null, "Urlop i nadgodziny?", default);
        var id = OnlyConversation();

        await runner.AskAsync(id, "Ile dni?", default);

        var history = _model.Requests.Last();
        Assert.Equal(
            new[] { ChatRole.User, ChatRole.Assistant, ChatRole.Tool, ChatRole.Assistant, ChatRole.User },
            history.Select(m => m.Role));
        Assert.Equal(new[] { "call_1", "call_2" }, history[1].Contents.OfType<FunctionCallContent>().Select(c => c.CallId));
        Assert.Equal(new[] { "call_1", "call_2" }, history[2].Contents.OfType<FunctionResultContent>().Select(c => c.CallId));
        Assert.Equal("Masz 7 dni i 4 godziny.", history[3].Text);
        Assert.Equal("Ile dni?", history[4].Text);
    }

    [Fact]
    public async Task UC25_Tool_error_result_is_passed_to_model_and_run_continues()
    {
        _toolbox.Results["get_leave_balance"] = new ToolOutcome("{\"error\":{\"code\":\"NotFound\",\"message\":\"Brak pracownika\"}}", true);
        _model.ThenToolCall("call_1", "get_leave_balance").ThenText("Nie mogę ustalić salda urlopu.");

        var result = await Runner().AskAsync(null, "Ile mam urlopu?", default);

        Assert.Equal("Nie mogę ustalić salda urlopu.", result.Reply!.Text);
        var passed = _model.Requests[1].SelectMany(m => m.Contents).OfType<FunctionResultContent>().Single();
        Assert.Contains("NotFound", passed.Result?.ToString());
    }

    [Fact]
    public async Task Unknown_tool_requested_by_model_is_handled_as_error_result()
    {
        _model.ThenToolCall("call_1", "delete_everything").ThenText("Nie mam takiego narzędzia.");

        var result = await Runner().AskAsync(null, "Usuń dane", default);

        Assert.Null(result.Failure);
        Assert.Empty(_toolbox.Calls);
        Assert.Equal("Nie mam takiego narzędzia.", result.Reply!.Text);
    }

    [Fact]
    public async Task Tool_loop_stops_after_6_rounds_and_returns_fallback_text()
    {
        _toolbox.Results["get_leave_balance"] = new ToolOutcome("{\"remainingDays\":7}", false);
        _model.RepeatLast = true;
        _model.ThenToolCall("call_x", "get_leave_balance");

        var result = await Runner().AskAsync(null, "Zapętl się", default);

        Assert.Null(result.Failure);
        Assert.False(string.IsNullOrWhiteSpace(result.Reply!.Text));
        Assert.InRange(_toolbox.Calls.Count, 1, 6);
        var stored = _store.Messages(OnlyConversation());
        Assert.Equal(StoredRole.Assistant, stored[^1].Role);
        Assert.Equal(result.Reply.Text, stored[^1].Content);
        Assert.Equal(stored.Count(m => m.Role == StoredRole.ToolCall), stored.Count(m => m.Role == StoredRole.ToolResult));
    }

    [Fact]
    public async Task Model_unavailable_keeps_question_and_returns_Unavailable()
    {
        _toolbox.Results["get_leave_balance"] = new ToolOutcome("{\"remainingDays\":7}", false);
        _model.ThenToolCall("call_1", "get_leave_balance").ThenThrow(new HttpRequestException("503"));

        var result = await Runner().AskAsync(null, "Ile mam urlopu?", default);

        Assert.Equal(AgentFailure.Unavailable, result.Failure);
        Assert.Null(result.Reply);
        Assert.Equal(new[] { StoredRole.User, StoredRole.ToolCall, StoredRole.ToolResult }, Roles(OnlyConversation()));
    }

    [Fact]
    public async Task Model_unavailable_on_first_call_keeps_only_the_question()
    {
        _model.ThenThrow(new HttpRequestException("503"));

        var result = await Runner().AskAsync(null, "Cześć", default);

        Assert.Equal(AgentFailure.Unavailable, result.Failure);
        Assert.Equal(new[] { StoredRole.User }, Roles(OnlyConversation()));
    }

    [Fact]
    public async Task UC27_Out_of_scope_answer_is_passed_through_without_tool_calls()
    {
        _model.ThenText("To pytanie wykracza poza mój zakres. Skontaktuj się z działem kadr.");

        var result = await Runner().AskAsync(null, "Jaka jutro będzie pogoda?", default);

        Assert.Empty(_toolbox.Calls);
        Assert.Equal(new[] { StoredRole.User, StoredRole.Assistant }, Roles(OnlyConversation()));
        Assert.Contains("działem kadr", result.Reply!.Text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_question_is_rejected_before_calling_model(string question)
    {
        var result = await Runner().AskAsync(null, question, default);

        Assert.Equal(AgentFailure.EmptyQuestion, result.Failure);
        Assert.Empty(_model.Requests);
        Assert.Empty(_store.ConversationIds);
    }

    [Fact]
    public async Task Question_over_4000_chars_is_rejected_before_calling_model()
    {
        var tooLong = await Runner().AskAsync(null, new string('a', 4001), default);
        _model.ThenText("ok");
        var exactly = await Runner().AskAsync(null, new string('a', 4000), default);

        Assert.Equal(AgentFailure.TooLong, tooLong.Failure);
        Assert.Null(exactly.Failure);
        Assert.Single(_model.Requests);
    }

    [Fact]
    public async Task Foreign_conversation_id_gives_NotFound_and_model_is_not_called()
    {
        var foreign = _store.AddForeignConversation();
        var unknown = Guid.NewGuid();

        var result = await Runner().AskAsync(foreign, "Cześć", default);
        var missing = await Runner().AskAsync(unknown, "Cześć", default);

        Assert.Equal(AgentFailure.NotFound, result.Failure);
        Assert.Equal(AgentFailure.NotFound, missing.Failure);
        Assert.Empty(_model.Requests);
        Assert.Empty(_store.Messages(foreign));
    }

    [Fact]
    public async Task Ungrounded_number_triggers_one_correction_round_then_grounded_answer_is_returned()
    {
        _model.ThenText("Zarobisz około 5000 zł brutto.")
              .ThenText("Naliczenie nie zostało jeszcze wykonane, więc nie podam kwoty.");

        var result = await Runner().AskAsync(null, "Ile zarobię w październiku?", default);

        Assert.Equal("Naliczenie nie zostało jeszcze wykonane, więc nie podam kwoty.", result.Reply!.Text);
        Assert.Empty(result.Reply.UngroundedNumbers);
        Assert.Equal(2, _model.Requests.Count);
        var correction = _model.Requests[1].Last();
        Assert.Equal(ChatRole.User, correction.Role);
        Assert.Contains("5000", correction.Text);
        Assert.Equal(new[] { StoredRole.User, StoredRole.Assistant }, Roles(OnlyConversation()));
        Assert.Equal("Naliczenie nie zostało jeszcze wykonane, więc nie podam kwoty.", _store.Messages(OnlyConversation())[^1].Content);
        Assert.Empty(_store.Warnings);
    }

    [Fact]
    public async Task Ungrounded_number_persisting_after_correction_is_returned_as_warning_only_once()
    {
        _model.ThenText("Zarobisz około 5000 zł.").ThenText("No dobrze, 5000 zł.");

        var result = await Runner().AskAsync(null, "Ile zarobię?", default);

        Assert.Equal("No dobrze, 5000 zł.", result.Reply!.Text);
        Assert.Equal(new[] { "5000" }, result.Reply.UngroundedNumbers);
        Assert.Equal(2, _model.Requests.Count);
        var warning = Assert.Single(_store.Warnings);
        Assert.Equal("AgentUngroundedNumbers", warning.EventType);
        Assert.Contains("5000", warning.Details);
        Assert.Equal(OnlyConversation(), warning.ConversationId);
    }

    [Fact]
    public async Task UC16_Agent_never_answers_pay_question_with_own_calculation_when_tool_reports_no_result()
    {
        _toolbox.Definitions.Add(new("get_payroll_result", "Naliczenie.", "{\"type\":\"object\",\"properties\":{}}"));
        _toolbox.Results["get_payroll_result"] = new ToolOutcome("{\"error\":{\"code\":\"NotFound\",\"message\":\"Naliczenie nie zostało jeszcze wykonane.\"}}", true);
        _model.ThenToolCall("call_1", "get_payroll_result")
              .ThenText("Przy pensji 8000 zł wyjdzie ok. 6000 zł netto.")
              .ThenText("Naliczenie nie zostało jeszcze wykonane, więc nie podam kwoty.");

        var result = await Runner().AskAsync(null, "Ile dostanę za październik?", default);

        Assert.DoesNotContain("6000", result.Reply!.Text);
        Assert.Empty(result.Reply.UngroundedNumbers);
        Assert.Equal(3, _model.Requests.Count);
    }

    [Fact]
    public async Task Number_taken_from_tool_result_is_not_flagged()
    {
        _toolbox.Results["get_leave_balance"] = new ToolOutcome("{\"remainingDays\":7}", false);
        _model.ThenToolCall("call_1", "get_leave_balance").ThenText("Zostało Ci 7 dni urlopu.");

        var result = await Runner().AskAsync(null, "Ile mam urlopu?", default);

        Assert.Empty(result.Reply!.UngroundedNumbers);
        Assert.Equal(2, _model.Requests.Count);
    }
}
