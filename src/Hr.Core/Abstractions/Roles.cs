namespace Hr.Core.Abstractions;

public static class Roles
{
    public const string Employee = "Employee";
    public const string Manager = "Manager";
    public const string HR = "HR";
    public const string HRApprover = "HRApprover";

    public static readonly IReadOnlyList<string> All = new[] { Employee, Manager, HR, HRApprover };
}
