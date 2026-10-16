using Hr.Agent;
using Hr.Core.AgentTools;
using Hr.TestSupport;

namespace Hr.Agent.Tests;

/// <summary>Pomijany, dopóki nie ustawisz <c>RUN_LIVE_SMOKE=1</c> i <c>ANTHROPIC_API_KEY</c>; łączy się z prawdziwym API.</summary>
public sealed class LiveFactAttribute : FactAttribute
{
    public LiveFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_LIVE_SMOKE") != "1" || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
        {
            Skip = "Test na prawdziwym API: ustaw RUN_LIVE_SMOKE=1 i ANTHROPIC_API_KEY.";
        }
    }
}

public class LiveSmokeTests
{
    [LiveFact]
    public async Task Real_model_calls_the_leave_balance_tool_and_answers_from_its_result()
    {
        var toolbox = new FakeToolbox();
        toolbox.Results["get_leave_balance"] = new ToolOutcome("{\"remainingDays\":17,\"entitlementDays\":26,\"approvedDays\":9,\"pendingDays\":0,\"ruleId\":\"KP-154-1\"}", false);
        var runner = new AgentRunner(AgentPipeline.Build(AgentPipeline.CreateAnthropic(new AnthropicOptions())), toolbox, new FakeConversationStore());

        var result = await runner.AskAsync(null, "Ile dni urlopu mi zostało?", CancellationToken.None);

        Assert.Null(result.Failure);
        Assert.Contains(toolbox.Calls, c => c.Tool == "get_leave_balance");
        Assert.Contains("17", result.Reply!.Text);
    }
}
