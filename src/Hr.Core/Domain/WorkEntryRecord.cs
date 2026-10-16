using Hr.Core.Security;
using Hr.Rules.Time;

namespace Hr.Core.Domain;

public class WorkEntryRecord : IEmployeeOwned
{
    public Guid Id { get; set; }

    public Guid EmployeeId { get; set; }

    public DateTime Start { get; set; }

    public DateTime End { get; set; }

    public TimeSpan Break { get; set; }

    public WorkEntry ToRulesEntry() => new(Start, End, Break);
}
