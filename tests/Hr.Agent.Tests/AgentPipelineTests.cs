using Hr.Agent;
using Hr.Core.AgentTools;
using Hr.TestSupport;
using Microsoft.Extensions.AI;

namespace Hr.Agent.Tests;

public class AgentPipelineTests
{
    private static ChatOptions OptionsFor(FakeToolbox toolbox) =>
        new() { Tools = ToolFunctionAdapter.For(toolbox, () => null, (_, _, _, _) => Task.CompletedTask).Cast<AITool>().ToList() };

    [Fact]
    public async Task Pipeline_builds_over_scripted_client()
    {
        var client = AgentPipeline.Build(new ScriptedChatClient().ThenText("Dzień dobry."));

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Cześć")]);

        Assert.Equal("Dzień dobry.", response.Text);
    }

    [Fact]
    public async Task Real_function_invocation_loop_executes_scripted_tool_call_and_returns_final_text()
    {
        var toolbox = new FakeToolbox();
        toolbox.Results["get_leave_balance"] = new ToolOutcome("{\"remainingDays\":7}", false);
        var model = new ScriptedChatClient()
            .ThenToolCall("call_1", "get_leave_balance")
            .ThenText("Zostało 7 dni.");
        var pipeline = AgentPipeline.Build(model);

        var response = await pipeline.GetResponseAsync([new ChatMessage(ChatRole.User, "Ile mam urlopu?")], OptionsFor(toolbox));

        Assert.Equal("Zostało 7 dni.", response.Text);
        Assert.Single(toolbox.Calls);
        Assert.Equal(2, model.Requests.Count);
        var result = model.Requests[1].SelectMany(m => m.Contents).OfType<FunctionResultContent>().Single();
        Assert.Equal("call_1", result.CallId);
        Assert.Contains("remainingDays", result.Result?.ToString());
    }

    [Fact]
    public async Task Loop_stops_after_max_rounds()
    {
        var toolbox = new FakeToolbox();
        toolbox.Results["get_leave_balance"] = new ToolOutcome("{\"remainingDays\":7}", false);
        var model = new ScriptedChatClient { RepeatLast = true }.ThenToolCall("call_x", "get_leave_balance");
        var pipeline = AgentPipeline.Build(model, maxRounds: 6);

        var response = await pipeline.GetResponseAsync([new ChatMessage(ChatRole.User, "Zapętl się")], OptionsFor(toolbox));

        Assert.InRange(toolbox.Calls.Count, 1, 6);
        Assert.NotNull(response);
    }

    [Fact]
    public async Task Missing_api_key_gives_ModelUnavailable_without_network()
    {
        string? saved = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
        try
        {
            var pipeline = AgentPipeline.Build(AgentPipeline.CreateAnthropic(new AnthropicOptions { ApiKey = null }));

            await Assert.ThrowsAsync<ModelUnavailableException>(() => pipeline.GetResponseAsync([new ChatMessage(ChatRole.User, "Cześć")]));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", saved);
        }
    }

    [Fact]
    public void Anthropic_client_is_created_with_key_without_network()
    {
        using var client = AgentPipeline.CreateAnthropic(new AnthropicOptions { ApiKey = "test-key" });

        Assert.NotNull(client);
    }
}
