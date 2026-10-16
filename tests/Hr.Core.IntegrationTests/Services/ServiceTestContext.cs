using Hr.Core.AgentTools;
using Hr.Core.Abstractions;
using Hr.Core.Auditing;
using Hr.Core.Security;
using Hr.Core.Persistence;
using Hr.Core.Services;
using Hr.Rules.Calendar;

namespace Hr.Core.IntegrationTests.Services;

/// <summary>Wspólne przygotowanie testów serwisów: baza, organizacja, zegar (2026-10-16 12:00 UTC) i użytkownicy.</summary>
public sealed class ServiceTestContext : IAsyncDisposable
{
    private readonly Dictionary<Guid, Guid> _userIds = new();

    private ServiceTestContext(HrDbContext db, TestOrg org, FakeClock clock)
    {
        Db = db;
        Org = org;
        Clock = clock;
    }

    public HrDbContext Db { get; }

    public TestOrg Org { get; }

    public FakeClock Clock { get; }

    public WorkCalendar Calendar { get; } = new();

    public static async Task<ServiceTestContext> CreateAsync(MsSqlFixture fixture, bool withRequests = false)
    {
        var clock = new FakeClock();
        var db = await fixture.NewContextAsync(clock: clock);
        var org = await TestOrg.SeedAsync(db, withRequests);
        return new ServiceTestContext(db, org, clock);
    }

    public Guid UserIdOf(Guid employeeId)
    {
        if (!_userIds.TryGetValue(employeeId, out var id))
        {
            _userIds[employeeId] = id = Guid.NewGuid();
        }

        return id;
    }

    public TestUser As(Guid employeeId, params string[] roles) => new(UserIdOf(employeeId), employeeId, roles);

    public LeaveService Leave(ICurrentUser user) => new(Db, user, Clock, Calendar);

    public TimesheetService Timesheet(ICurrentUser user) => new(Db, user, Clock, Calendar);

    public PayrollChangeService PayrollChanges(ICurrentUser user) => new(Db, user, Clock);

    public CorrectionDraftService Drafts(ICurrentUser user) => new(Db, user, Clock, Timesheet(user));

    public AgentToolbox Toolbox(ICurrentUser user) => new(
        Db, user, Clock, Leave(user), Timesheet(user), Payroll(user), Drafts(user), new LabourCodeSearch(Db), new EmployeeDirectoryService(Db, user), new AuditWriter(Db, user, Clock));

    public ConversationStore Store(ICurrentUser user) => Store(Db, user);

    public ConversationStore Store(HrDbContext db, ICurrentUser user)
    {
        var writer = new AuditWriter(db, user, Clock);
        return new ConversationStore(db, user, Clock, new ConversationReader(db, writer, user), writer);
    }

    public PayrollService Payroll(ICurrentUser user) => new(Db, user, Clock, Calendar);

    public ValueTask DisposeAsync() => Db.DisposeAsync();
}
