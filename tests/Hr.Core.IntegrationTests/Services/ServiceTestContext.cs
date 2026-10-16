using Hr.Core.Abstractions;
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

    public ValueTask DisposeAsync() => Db.DisposeAsync();
}
