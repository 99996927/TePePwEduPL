using Hr.Core.Abstractions;
using Hr.Core.Auditing;
using Hr.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.IntegrationTests.Services;

[Collection("mssql")]
public class AuditServiceTests(MsSqlFixture fixture)
{
    private static readonly Guid U1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid U2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static async Task RecordAsync(ServiceTestContext c, Guid actor, string eventType, string entityType, string entityId, DateTime atUtc)
    {
        c.Clock.UtcNow = atUtc;
        new AuditWriter(c.Db, new TestUser(actor, null), c.Clock).Record(eventType, entityType, entityId);
        await c.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task UC28_Only_hr_roles_can_query_audit()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);

        Assert.True((await new AuditService(c.Db, c.As(c.Org.Piotr, Roles.HR)).QueryAsync(new AuditQuery())).IsSuccess);
        Assert.True((await new AuditService(c.Db, c.As(c.Org.Maria, Roles.HRApprover)).QueryAsync(new AuditQuery())).IsSuccess);
        Assert.Equal(ServiceErrorCode.Forbidden, (await new AuditService(c.Db, c.As(c.Org.Anna, Roles.Employee)).QueryAsync(new AuditQuery())).Error!.Code);
        Assert.Equal(ServiceErrorCode.Forbidden, (await new AuditService(c.Db, c.As(c.Org.Ewa, Roles.Manager)).QueryAsync(new AuditQuery())).Error!.Code);
    }

    [Fact]
    public async Task UC28_Query_filters_by_entity_actor_and_range_newest_first()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var t = new DateTime(2026, 10, 16, 9, 0, 0, DateTimeKind.Utc);
        await RecordAsync(c, U1, "A", "Probe", "1", t);
        await RecordAsync(c, U2, "B", "Probe", "2", t.AddHours(1));
        await RecordAsync(c, U1, "C", "Other", "3", t.AddHours(2));
        await RecordAsync(c, U1, "D", "Probe", "4", t.AddHours(3));
        var audit = new AuditService(c.Db, c.As(c.Org.Piotr, Roles.HR));

        var probe = (await audit.QueryAsync(new AuditQuery(EntityType: "Probe"))).Value!;
        var byActor = (await audit.QueryAsync(new AuditQuery(EntityType: "Probe", ActorUserId: U1))).Value!;
        var ranged = (await audit.QueryAsync(new AuditQuery(EntityType: "Probe", FromUtc: t.AddHours(1), ToUtc: t.AddHours(2)))).Value!;
        var byId = (await audit.QueryAsync(new AuditQuery(EntityType: "Probe", EntityId: "2"))).Value!;

        Assert.Equal(new[] { "D", "B", "A" }, probe.Items.Select(a => a.EventType));
        Assert.Equal(new[] { "D", "A" }, byActor.Items.Select(a => a.EventType));
        Assert.Equal(new[] { "B" }, ranged.Items.Select(a => a.EventType));
        Assert.Equal(new[] { "B" }, byId.Items.Select(a => a.EventType));
    }

    [Fact]
    public async Task UC28_Paging_and_validation()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var t = new DateTime(2026, 10, 16, 9, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 250; i++)
        {
            c.Db.AuditLogs.Add(new Hr.Core.Domain.AuditLog { ActorUserId = U1, AtUtc = t, EventType = "E", EntityType = "Paging", EntityId = i.ToString() });
        }

        await c.Db.SaveChangesAsync();
        var audit = new AuditService(c.Db, c.As(c.Org.Piotr, Roles.HR));

        var capped = (await audit.QueryAsync(new AuditQuery(EntityType: "Paging", Take: 500))).Value!;
        var page = (await audit.QueryAsync(new AuditQuery(EntityType: "Paging", Skip: 240, Take: 20))).Value!;

        Assert.Equal(250, capped.Total);
        Assert.Equal(200, capped.Items.Count);
        Assert.Equal(250, page.Total);
        Assert.Equal(10, page.Items.Count);
        Assert.Equal(ServiceErrorCode.Validation, (await audit.QueryAsync(new AuditQuery(Skip: -1))).Error!.Code);
        Assert.Equal(ServiceErrorCode.Validation, (await audit.QueryAsync(new AuditQuery(Take: 0))).Error!.Code);
    }
}
