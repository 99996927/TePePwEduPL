using System.Text.Json;
using Hr.Agent;
using Hr.Core.AgentTools;
using Hr.TestSupport;
using Microsoft.Extensions.AI;

namespace Hr.Agent.Tests;

public class ToolFunctionAdapterTests
{
    private static readonly Func<string, string, string, string, Task> Ignore = (_, _, _, _) => Task.CompletedTask;

    [Fact]
    public void Functions_mirror_toolbox_definitions_name_description_schema()
    {
        var toolbox = new FakeToolbox();

        var functions = ToolFunctionAdapter.For(toolbox, () => null, Ignore);

        Assert.Equal(toolbox.Definitions.Select(d => d.Name), functions.Select(f => f.Name));
        Assert.Equal(toolbox.Definitions.Select(d => d.Description), functions.Select(f => f.Description));
        var overtime = functions.Single(f => f.Name == "calculate_overtime");
        Assert.Equal("object", overtime.JsonSchema.GetProperty("type").GetString());
        Assert.True(overtime.JsonSchema.GetProperty("properties").TryGetProperty("from", out _));
    }

    [Fact]
    public async Task Invocation_passes_arguments_as_json_and_returns_tool_json()
    {
        var toolbox = new FakeToolbox();
        toolbox.Results["calculate_overtime"] = new ToolOutcome("{\"overtimeTotal\":4.5}", false);
        var conversation = Guid.NewGuid();
        var function = ToolFunctionAdapter.For(toolbox, () => conversation, Ignore).Single(f => f.Name == "calculate_overtime");

        var result = await function.InvokeAsync(new AIFunctionArguments { ["from"] = "2026-10-01", ["to"] = "2026-10-31" });

        var call = Assert.Single(toolbox.Calls);
        Assert.Equal("calculate_overtime", call.Tool);
        Assert.Equal(conversation, call.ConversationId);
        using var input = JsonDocument.Parse(call.Input);
        Assert.Equal("2026-10-01", input.RootElement.GetProperty("from").GetString());
        Assert.Equal("2026-10-31", input.RootElement.GetProperty("to").GetString());
        Assert.Equal(4.5m, ((JsonElement)result!).GetProperty("overtimeTotal").GetDecimal());
    }

    [Fact]
    public async Task Tool_error_is_returned_as_result_not_exception()
    {
        var toolbox = new FakeToolbox();
        toolbox.Results["get_leave_balance"] = new ToolOutcome("{\"error\":{\"code\":\"NotFound\",\"message\":\"Brak\"}}", true);
        var function = ToolFunctionAdapter.For(toolbox, () => null, Ignore).Single(f => f.Name == "get_leave_balance");

        var result = await function.InvokeAsync(new AIFunctionArguments());

        Assert.Equal("NotFound", ((JsonElement)result!).GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task OnInvoked_receives_pair_with_call_id_after_execution()
    {
        var toolbox = new FakeToolbox();
        toolbox.Results["get_leave_balance"] = new ToolOutcome("{\"remainingDays\":7}", false);
        var seen = new List<(string Id, string Tool, string Input, string Result, int CallsSoFar)>();
        var function = ToolFunctionAdapter
            .For(toolbox, () => null, (id, tool, input, result) =>
            {
                seen.Add((id, tool, input, result, toolbox.Calls.Count));
                return Task.CompletedTask;
            })
            .Single(f => f.Name == "get_leave_balance");

        await function.InvokeAsync(new AIFunctionArguments());

        var pair = Assert.Single(seen);
        Assert.False(string.IsNullOrWhiteSpace(pair.Id));
        Assert.Equal("get_leave_balance", pair.Tool);
        Assert.Equal("{}", pair.Input);
        Assert.Equal("{\"remainingDays\":7}", pair.Result);
        Assert.Equal(1, pair.CallsSoFar);
    }

    [Fact]
    public void Definitions_are_taken_per_current_user()
    {
        var toolbox = new FakeToolbox();
        var employeeView = ToolFunctionAdapter.For(toolbox, () => null, Ignore).Select(f => f.Name).ToList();
        toolbox.Definitions.Add(new("propose_correction", "Szkic korekty.", "{\"type\":\"object\",\"properties\":{}}"));

        var hrView = ToolFunctionAdapter.For(toolbox, () => null, Ignore).Select(f => f.Name).ToList();

        Assert.DoesNotContain("propose_correction", employeeView);
        Assert.Contains("propose_correction", hrView);
    }
}
