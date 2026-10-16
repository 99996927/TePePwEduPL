using Hr.Core.Abstractions;
using Hr.Core.Auditing;
using Hr.Core.Domain;
using Hr.Core.Persistence;
using Hr.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.IntegrationTests;

[Collection("mssql")]
public class ConversationAccessTests(MsSqlFixture fixture)
{
    private static readonly DateTime Now = new(2026, 10, 16, 9, 0, 0, DateTimeKind.Utc);

    private sealed record Setup(HrDbContext Db, TestOrg Org, Guid AnnaUser, Guid ConversationId);

    private async Task<Setup> ArrangeAsync()
    {
        var db = await fixture.NewContextAsync();
        var org = await TestOrg.SeedAsync(db);
        var annaUser = Guid.NewGuid();
        var conversation = new Conversation { Id = Guid.NewGuid(), UserId = annaUser, StartedAtUtc = Now };
        db.Conversations.Add(conversation);
        db.Messages.Add(new Message { Id = Guid.NewGuid(), ConversationId = conversation.Id, Seq = 2, Role = MessageRole.Assistant, Content = "Masz 2 h.", CreatedAtUtc = Now });
        db.Messages.Add(new Message { Id = Guid.NewGuid(), ConversationId = conversation.Id, Seq = 1, Role = MessageRole.User, Content = "Ile nadgodzin?", CreatedAtUtc = Now });
        await db.SaveChangesAsync();
        return new Setup(db, org, annaUser, conversation.Id);
    }

    private static ConversationReader ReaderFor(HrDbContext db, ICurrentUser user) =>
        new(db, new AuditWriter(db, user, new FakeClock()), user);

    private static Task<int> ReadAudits(HrDbContext db) => db.AuditLogs.CountAsync(a => a.EventType == "ConversationRead");

    [Fact]
    public async Task UC30_Author_reads_own_conversation_without_audit()
    {
        var s = await ArrangeAsync();
        await using var db = s.Db;
        var anna = new TestUser(s.AnnaUser, s.Org.Anna, Roles.Employee);

        var conversation = await ReaderFor(db, anna).OpenAsync(s.ConversationId);

        Assert.NotNull(conversation);
        Assert.Equal(new[] { 1, 2 }, conversation!.Messages.Select(m => m.Seq));
        Assert.Equal(0, await ReadAudits(db));
    }

    [Fact]
    public async Task UC30_Employee_cannot_open_other_employees_conversation()
    {
        var s = await ArrangeAsync();
        await using var db = s.Db;
        var tomasz = new TestUser(Guid.NewGuid(), s.Org.Tomasz, Roles.Employee);

        Assert.Null(await ReaderFor(db, tomasz).OpenAsync(s.ConversationId));
        Assert.Equal(0, await ReadAudits(db));
    }

    [Fact]
    public async Task Q08_Manager_cannot_open_team_members_conversation()
    {
        var s = await ArrangeAsync();
        await using var db = s.Db;
        var ewa = new TestUser(Guid.NewGuid(), s.Org.Ewa, Roles.Manager);

        Assert.Null(await ReaderFor(db, ewa).OpenAsync(s.ConversationId));
    }

    [Fact]
    public async Task UC31_HR_opens_any_conversation_and_read_is_audited()
    {
        var s = await ArrangeAsync();
        await using var db = s.Db;
        var piotrUser = Guid.NewGuid();
        var piotr = new TestUser(piotrUser, s.Org.Piotr, Roles.HR);

        var conversation = await ReaderFor(db, piotr).OpenAsync(s.ConversationId);

        Assert.NotNull(conversation);
        Assert.Equal(new[] { 1, 2 }, conversation!.Messages.Select(m => m.Seq));
        var audit = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.EventType == "ConversationRead");
        Assert.Equal(piotrUser, audit.ActorUserId);
        Assert.Equal("Conversation", audit.EntityType);
        Assert.Equal(s.ConversationId.ToString(), audit.EntityId);
    }

    [Fact]
    public async Task UC31_Approver_can_read_too()
    {
        var s = await ArrangeAsync();
        await using var db = s.Db;
        var maria = new TestUser(Guid.NewGuid(), s.Org.Maria, Roles.HRApprover);

        Assert.NotNull(await ReaderFor(db, maria).OpenAsync(s.ConversationId));
        Assert.Equal(1, await ReadAudits(db));
    }

    [Fact]
    public async Task UC30_User_without_id_sees_no_conversations()
    {
        var s = await ArrangeAsync();
        await using var db = s.Db;
        var nobody = new TestUser(null, null, Roles.HR);

        Assert.Null(await ReaderFor(db, nobody).OpenAsync(s.ConversationId));
        Assert.Empty(await db.Conversations.Visible(nobody).ToListAsync());
        Assert.Equal(0, await ReadAudits(db));
    }
}
