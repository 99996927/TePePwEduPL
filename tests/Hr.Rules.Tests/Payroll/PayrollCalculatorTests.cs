using Hr.Rules.Payroll;

namespace Hr.Rules.Tests.Payroll;

public class PayrollCalculatorTests
{
    [Fact]
    public void UC16_No_overtime_gross_equals_base()
    {
        var r = PayrollCalculator.Calculate(new PayrollInput(8000m, 176m, 0m, 0m));
        Assert.Equal(8000m, r.Gross);
        Assert.Equal(0m, r.Allowance50);
    }

    [Fact]
    public void UC16_Overtime_allowances_are_computed_from_unrounded_rate()
    {
        var r = PayrollCalculator.Calculate(new PayrollInput(8000m, 176m, 2m, 1m));
        Assert.Equal(45.45m, r.HourlyRate);
        Assert.Equal(45.45m, r.Allowance50);
        Assert.Equal(45.45m, r.Allowance100);
        Assert.Equal(8090.90m, r.Gross);
    }

    [Fact]
    public void UC16_RuleIds_reference_overtime_premiums()
    {
        var r = PayrollCalculator.Calculate(new PayrollInput(8000m, 176m, 2m, 1m));
        Assert.Contains("KP-151-1-1", r.RuleIds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void UC16_Norm_hours_must_be_positive(int norm) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PayrollCalculator.Calculate(new PayrollInput(8000m, norm, 0m, 0m)));

    [Fact]
    public void UC16_Negative_overtime_throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PayrollCalculator.Calculate(new PayrollInput(8000m, 176m, -1m, 0m)));

    [Fact]
    public void UC16_Calculate_is_deterministic() =>
        Assert.Equal(
            PayrollCalculator.Calculate(new PayrollInput(8000m, 176m, 2m, 1m)),
            PayrollCalculator.Calculate(new PayrollInput(8000m, 176m, 2m, 1m)));
}
