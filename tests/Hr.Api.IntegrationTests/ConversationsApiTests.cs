using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Hr.Api.IntegrationTests;

[Collection("api")]
public class ConversationsApiTests(MsSqlFixture fixture)
{
    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static Task<HttpResponseMessage> Ask(ApiSession s, string text, Guid? conversationId = null) =>
        s.PostAsync("/api/conversations/messages", new { conversationId, text });

    private static async Task<Guid> EmployeeIdOf(ApiSession session) =>
        (await JsonOf(await session.GetAsync("/api/auth/me"))).GetProperty("employeeId").GetGuid();

    private static string Field(IEnumerable<ChatMessage> messages, string property)
    {
        var result = messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Last();
        return ((JsonElement)result.Result!).GetProperty(property).GetRawText();
    }

    private static async Task AddOvertimeDay(ApiSession anna) =>
        Assert.Equal(HttpStatusCode.Created, (await anna.PostAsync("/api/timesheet/entries",
            new { start = "2026-10-06T08:00:00", end = "2026-10-06T20:00:00", breakMinutes = 0 })).StatusCode);

    private static Dictionary<string, object?> Period => new() { ["from"] = "2026-10-01", ["to"] = "2026-10-31" };

    [Fact]
    public async Task UC22_Employee_asks_about_overtime_and_gets_answer_based_on_tool_result()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        await AddOvertimeDay(anna);
        factory.Model.ThenToolCall("call_1", "calculate_overtime", Period)
            .Then(messages => new ChatResponse(new ChatMessage(ChatRole.Assistant, $"Masz {Field(messages, "overtimeTotal")} godzin nadgodzin.")));

        var response = await Ask(anna, "Ile mam nadgodzin w październiku?");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonOf(response);
        Assert.Contains("4", body.GetProperty("text").GetString());
        Assert.Empty(body.GetProperty("ungroundedNumbers").EnumerateArray());
        var id = body.GetProperty("conversationId").GetGuid();
        var detail = await JsonOf(await anna.GetAsync($"/api/conversations/{id}"));
        var messages = detail.GetProperty("messages").EnumerateArray().ToList();
        Assert.Equal(new[] { "User", "ToolCall", "ToolResult", "Assistant" }, messages.Select(m => m.GetProperty("role").GetString()));
        Assert.Equal("calculate_overtime", messages[1].GetProperty("toolName").GetString());
        Assert.Contains("overtimeTotal", messages[2].GetProperty("content").GetString());
        Assert.Equal(messages.Count, body.GetProperty("messages").GetArrayLength());
    }

    [Fact]
    public async Task UC24_Employee_asking_for_other_employee_gets_own_data_only()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var jan = await ApiSession.LoginAsAsync(factory, "jan.kowalski");
        var janId = await EmployeeIdOf(jan);
        var annaId = await EmployeeIdOf(anna);
        factory.Model.ThenToolCall("call_1", "get_leave_balance", new Dictionary<string, object?> { ["employee_id"] = janId.ToString() })
            .ThenText("Oto Twoje saldo urlopu.");

        var body = await JsonOf(await Ask(anna, "Pokaż urlop Jana Kowalskiego"));

        var detail = await JsonOf(await anna.GetAsync($"/api/conversations/{body.GetProperty("conversationId").GetGuid()}"));
        string result = detail.GetProperty("messages").EnumerateArray().Single(m => m.GetProperty("role").GetString() == "ToolResult").GetProperty("content").GetString()!;
        Assert.Contains(annaId.ToString(), result);
        Assert.DoesNotContain(janId.ToString(), result);
    }

    [Fact]
    public async Task UC25_Tool_error_is_reported_in_answer()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        factory.Model.ThenToolCall("call_1", "calculate_overtime", new Dictionary<string, object?> { ["from"] = "2026-10-31", ["to"] = "2026-10-01" })
            .Then(messages => new ChatResponse(new ChatMessage(ChatRole.Assistant, Field(messages, "error").Contains("Validation") ? "Nie mogę policzyć nadgodzin: okres jest nieprawidłowy." : "?")));

        var body = await JsonOf(await Ask(anna, "Nadgodziny od końca do początku?"));

        Assert.Contains("nieprawidłowy", body.GetProperty("text").GetString());
        Assert.Empty(body.GetProperty("ungroundedNumbers").EnumerateArray());
    }

    [Fact]
    public async Task UC27_Out_of_scope_is_polite_refusal()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        factory.Model.ThenText("To wykracza poza mój zakres. Skontaktuj się z działem kadr.");

        var body = await JsonOf(await Ask(anna, "Jaka będzie pogoda?"));

        Assert.Contains("działem kadr", body.GetProperty("text").GetString());
        Assert.Equal(new[] { "User", "Assistant" }, body.GetProperty("messages").EnumerateArray().Select(m => m.GetProperty("role").GetString()));
    }

    [Fact]
    public async Task UC23_Labour_code_tool_result_contains_citation()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        factory.Model.ThenToolCall("call_1", "search_labour_code", new Dictionary<string, object?> { ["query"] = "art. 131" })
            .ThenText("Zgodnie z art. 131 Kodeksu pracy tygodniowy czas pracy nie przekracza przeciętnie 48 godzin.");

        var body = await JsonOf(await Ask(anna, "Ile godzin można pracować tygodniowo?"));

        Assert.Empty(body.GetProperty("ungroundedNumbers").EnumerateArray());
        var result = body.GetProperty("messages").EnumerateArray().Single(m => m.GetProperty("role").GetString() == "ToolResult").GetProperty("content").GetString()!;
        Assert.Contains("art. 131 Kodeksu pracy (Dz.U. 2026 poz. 1245)", result);
    }

    [Fact]
    public async Task UC30_Employee_sees_only_own_conversations_and_can_continue_one()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var jan = await ApiSession.LoginAsAsync(factory, "jan.kowalski");
        factory.Model.ThenText("Pierwsza odpowiedź.").ThenText("Druga odpowiedź.").ThenText("Odpowiedź Jana.");
        var first = await JsonOf(await Ask(anna, "Pierwsze pytanie"));
        var id = first.GetProperty("conversationId").GetGuid();

        var second = await JsonOf(await Ask(anna, "Drugie pytanie", id));
        await Ask(jan, "Pytanie Jana");

        Assert.Equal(id, second.GetProperty("conversationId").GetGuid());
        var history = factory.Model.Requests[1];
        Assert.Equal(new[] { ChatRole.User, ChatRole.Assistant, ChatRole.User }, history.Select(m => m.Role));
        Assert.Equal("Pierwsze pytanie", history[0].Text);
        var annaList = await JsonOf(await anna.GetAsync("/api/conversations"));
        var janList = await JsonOf(await jan.GetAsync("/api/conversations"));
        Assert.Equal(id, annaList.EnumerateArray().Single().GetProperty("id").GetGuid());
        Assert.Equal(4, annaList.EnumerateArray().Single().GetProperty("messageCount").GetInt32());
        Assert.Equal("Pierwsze pytanie", annaList.EnumerateArray().Single().GetProperty("preview").GetString());
        Assert.Equal("Pytanie Jana", janList.EnumerateArray().Single().GetProperty("preview").GetString());
    }

    [Fact]
    public async Task UC31_HR_reads_foreign_conversation_and_audit_records_it()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var piotr = await ApiSession.LoginAsAsync(factory, "piotr.zielinski");
        factory.Model.ThenText("Odpowiedź.");
        var id = (await JsonOf(await Ask(anna, "Pytanie Anny"))).GetProperty("conversationId").GetGuid();

        var detail = await piotr.GetAsync($"/api/conversations/{id}");
        var all = await JsonOf(await piotr.GetAsync("/api/conversations"));
        var audit = await JsonOf(await piotr.GetAsync($"/api/audit?entityType=Conversation&entityId={id}"));

        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal(2, (await JsonOf(detail)).GetProperty("messages").GetArrayLength());
        Assert.Contains(all.EnumerateArray(), c => c.GetProperty("id").GetGuid() == id);
        Assert.Contains(audit.GetProperty("items").EnumerateArray(), a => a.GetProperty("eventType").GetString() == "ConversationRead");
    }

    [Fact]
    public async Task Q08_Manager_and_other_employee_get_404_on_foreign_conversation()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var ewa = await ApiSession.LoginAsAsync(factory, "ewa.wisniewska");
        var jan = await ApiSession.LoginAsAsync(factory, "jan.kowalski");
        factory.Model.ThenText("Odpowiedź.");
        var id = (await JsonOf(await Ask(anna, "Pytanie Anny"))).GetProperty("conversationId").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await ewa.GetAsync($"/api/conversations/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await jan.GetAsync($"/api/conversations/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Ask(jan, "Wtargnięcie", id)).StatusCode);
        Assert.Empty((await JsonOf(await ewa.GetAsync("/api/conversations"))).EnumerateArray());
    }

    [Fact]
    public async Task Agent_unavailable_gives_503_problem_with_code_and_keeps_question()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        factory.Model.ThenThrow(new HttpRequestException("503 Service Unavailable"));

        var response = await Ask(anna, "Czy to działa?");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await JsonOf(response);
        Assert.Equal("AgentUnavailable", problem.GetProperty("code").GetString());
        var id = problem.GetProperty("conversationId").GetGuid();
        var detail = await JsonOf(await anna.GetAsync($"/api/conversations/{id}"));
        var message = detail.GetProperty("messages").EnumerateArray().Single();
        Assert.Equal("Czy to działa?", message.GetProperty("content").GetString());
        Assert.DoesNotContain("503 Service", problem.GetRawText());
    }

    [Fact]
    public async Task Missing_api_key_in_real_client_gives_503_not_500()
    {
        string? saved = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
        try
        {
            await using var factory = await HrApiFactory.CreateAsync(fixture, scriptedModel: false);
            var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");

            var response = await Ask(anna, "Cześć");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("AgentUnavailable", (await JsonOf(response)).GetProperty("code").GetString());
            Assert.Equal(HttpStatusCode.OK, (await anna.GetAsync("/api/leave/balance")).StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", saved);
        }
    }

    [Fact]
    public async Task Ungrounded_number_in_answer_is_returned_as_warning_and_audited()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var piotr = await ApiSession.LoginAsAsync(factory, "piotr.zielinski");
        factory.Model.ThenText("Zarobisz około 5000 zł.").ThenText("Nadal uważam, że 5000 zł.");

        var body = await JsonOf(await Ask(anna, "Ile zarobię?"));

        Assert.Equal(new[] { "5000" }, body.GetProperty("ungroundedNumbers").EnumerateArray().Select(n => n.GetString()));
        var id = body.GetProperty("conversationId").GetGuid();
        var audit = await JsonOf(await piotr.GetAsync($"/api/audit?entityType=Conversation&entityId={id}"));
        Assert.Contains(audit.GetProperty("items").EnumerateArray(), a => a.GetProperty("eventType").GetString() == "AgentUngroundedNumbers");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Question_validation_422_for_empty_text(string text)
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");

        var response = await Ask(anna, text);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("Validation", (await JsonOf(response)).GetProperty("code").GetString());
        Assert.Empty(factory.Model.Requests);
    }

    [Fact]
    public async Task Question_validation_422_for_text_over_4000_chars()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");

        var response = await Ask(anna, new string('a', 4001));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Empty(factory.Model.Requests);
    }

    [Fact]
    public async Task Unauthenticated_is_401()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var session = await ApiSession.StartAsync(factory);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Ask(session, "Cześć")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/conversations")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync($"/api/conversations/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task CSRF_token_required_for_post()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");

        var response = await anna.SendAsync(HttpMethod.Post, "/api/conversations/messages", new { text = "Cześć" }, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.Model.Requests);
    }
}
