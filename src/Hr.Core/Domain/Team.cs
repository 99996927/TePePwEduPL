namespace Hr.Core.Domain;

public class Team
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid? ManagerEmployeeId { get; set; }
}
