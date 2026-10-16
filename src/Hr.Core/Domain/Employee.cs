namespace Hr.Core.Domain;

public class Employee
{
    public Guid Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public DateOnly HireDate { get; set; }

    public Guid TeamId { get; set; }

    public decimal MonthlySalary { get; set; }
}
