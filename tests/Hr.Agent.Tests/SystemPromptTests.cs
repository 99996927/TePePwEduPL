using Hr.Agent;

namespace Hr.Agent.Tests;

public class SystemPromptTests
{
    [Fact]
    public void Prompt_requires_digits_so_number_guard_can_check_every_number()
    {
        Assert.Contains("wyłącznie cyframi", SystemPrompt.Text);
        Assert.Contains("słownie", SystemPrompt.Text);
    }

    [Fact]
    public void Prompt_points_hr_to_lookup_tools_instead_of_guessing_identifiers()
    {
        Assert.Contains("find_employee", SystemPrompt.Text);
        Assert.Contains("list_work_entries", SystemPrompt.Text);
    }

    [Fact]
    public void Prompt_keeps_the_no_own_calculation_rule()
    {
        Assert.Contains("Nigdy nie liczysz sam", SystemPrompt.Text);
        Assert.Contains("get_payroll_result", SystemPrompt.Text);
    }
}
