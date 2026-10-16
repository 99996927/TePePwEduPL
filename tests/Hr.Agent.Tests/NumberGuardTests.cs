using Hr.Agent;

namespace Hr.Agent.Tests;

public class NumberGuardTests
{
    [Fact]
    public void Numbers_from_tool_results_are_grounded()
    {
        var sources = new[] { "{\"overtimeTotal\":12.50,\"remainingDays\":7,\"gross\":8123.40}" };

        Assert.Empty(NumberGuard.FindUngrounded("Masz 12,5 godziny nadgodzin i 7 dni urlopu. Brutto: 8 123,40 zł.", sources));
    }

    [Fact]
    public void Number_not_in_any_source_is_reported()
    {
        var ungrounded = NumberGuard.FindUngrounded("Zarobisz około 5000 zł, czyli 7 dni.", new[] { "{\"remainingDays\":7}" });

        Assert.Equal(new[] { "5000" }, ungrounded);
    }

    [Fact]
    public void Sum_of_two_results_is_not_grounded()
    {
        Assert.Equal(new[] { "10" }, NumberGuard.FindUngrounded("Razem masz 10 godzin.", new[] { "{\"a\":4}", "{\"b\":6}" }));
    }

    [Fact]
    public void Article_numbers_and_list_markers_are_ignored()
    {
        const string answer = "Zgodnie z art. 129 § 2 oraz art. 151[1] § 1 i ust. 3 pkt 4:\n1) pierwsza zasada;\n2. druga zasada.";

        Assert.Empty(NumberGuard.FindUngrounded(answer, Array.Empty<string>()));
    }

    [Fact]
    public void Dates_in_tool_results_ground_their_parts()
    {
        var sources = new[] { "{\"from\":\"2026-10-31\",\"to\":\"2026-10-31\"}" };

        Assert.Empty(NumberGuard.FindUngrounded("Okres do 31.10.2026.", sources));
        Assert.Equal(new[] { "2026" }, NumberGuard.FindUngrounded("Okres do 31.10.2026.", new[] { "{\"x\":\"31\",\"y\":\"10\"}" }).Select(n => n));
    }

    [Fact]
    public void Numbers_from_user_question_are_allowed()
    {
        Assert.Empty(NumberGuard.FindUngrounded("Pytasz o 14 dni urlopu.", new[] { "Chcę wziąć 14 dni urlopu" }));
    }

    [Fact]
    public void Digits_inside_json_keys_and_rule_ids_do_not_ground_numbers()
    {
        // „overtimeHours50” i „allowance100” to nazwy pól, a nie wartości.
        Assert.Equal(new[] { "50" }, NumberGuard.FindUngrounded("Masz 50 godzin.", new[] { "{\"overtimeHours50\":0,\"allowance100\":0}" }));
    }

    [Fact]
    public void Rule_ids_quoted_from_tool_results_are_grounded()
    {
        Assert.Empty(NumberGuard.FindUngrounded("Reguła KP-151-1.", new[] { "{\"ruleIds\":[\"KP-151-1\"]}" }));
    }

    [Fact]
    public void Thousands_with_spaces_are_one_number()
    {
        Assert.Empty(NumberGuard.FindUngrounded("Brutto: 12 345,50 zł.", new[] { "{\"gross\":12345.5}" }));
    }

    [Fact]
    public void Repeated_ungrounded_number_is_reported_once()
    {
        Assert.Equal(new[] { "99" }, NumberGuard.FindUngrounded("99 i jeszcze raz 99.", Array.Empty<string>()));
    }
}
