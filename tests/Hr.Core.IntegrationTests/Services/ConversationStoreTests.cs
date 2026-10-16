using Hr.Core.Abstractions;
using Hr.Core.AgentTools;
using Hr.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.IntegrationTests.Services;

[Collection("mssql")]
public class ConversationStoreTests(MsSqlFixture fixture)
{
    private static async Task<Guid> StartAs(ConversationStore store)
    {
        var started = await store.StartAsync(CancellationToken.None);
        Assert.True(started.IsSuccess);
        return started.Value;
    }

    private static Task<StoreResult<StoredMessage>> Say(ConversationStore store, Guid id, StoredRole role, string content, string? tool = null, string? json = null) =>
        store.AppendAsync(id, role, content, tool, json, CancellationToken.None);

    [Fact]
    public async Task UC30_Start_creates_conversation_owned_by_current_user()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var id = await StartAs(c.Store(c.As(c.Org.Anna, Roles.Employee)));

        var row = await c.Db.Conversations.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(c.UserIdOf(c.Org.Anna), row.UserId);
        Assert.Equal(c.Clock.UtcNow, row.StartedAtUtc);
    }

    [Fact]
    public async Task UC30_Append_assigns_increasing_seq_and_load_returns_in_order()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var store = c.Store(c.As(c.Org.Anna, Roles.Employee));
        var id = await StartAs(store);

        var first = await Say(store, id, StoredRole.User, "Ile mam nadgodzin?");
        var second = await Say(store, id, StoredRole.ToolCall, "toolu_1", "calculate_overtime", "{\"from\":\"2026-10-01\"}");
        var third = await Say(store, id, StoredRole.ToolResult, "{\"overtimeTotal\":4}", "calculate_overtime");
        var fourth = await Say(store, id, StoredRole.Assistant, "Masz 4 godziny.");

        Assert.Equal(new[] { 1, 2, 3, 4 }, new[] { first, second, third, fourth }.Select(r => r.Value!.Seq));
        var loaded = (await store.LoadAsync(id, 40, CancellationToken.None)).Value!;
        Assert.Equal(new[] { "Ile mam nadgodzin?", "toolu_1", "{\"overtimeTotal\":4}", "Masz 4 godziny." }, loaded.Select(m => m.Content));
        Assert.Equal("calculate_overtime", loaded[1].ToolName);
        Assert.Equal("{\"from\":\"2026-10-01\"}", loaded[1].ToolParamsJson);
    }

    [Fact]
    public async Task UC30_List_returns_only_own_conversations_for_employee_and_manager()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var anna = c.Store(c.As(c.Org.Anna, Roles.Employee));
        var ewa = c.Store(c.As(c.Org.Ewa, Roles.Manager));
        var annaId = await StartAs(anna);
        await Say(anna, annaId, StoredRole.User, new string('x', 200));
        await Say(anna, annaId, StoredRole.Assistant, "odpowiedź");
        var ewaId = await StartAs(ewa);
        await Say(ewa, ewaId, StoredRole.User, "Pytanie kierownika");

        var annaList = await anna.ListAsync(CancellationToken.None);
        var ewaList = await ewa.ListAsync(CancellationToken.None);

        var mine = Assert.Single(annaList);
        Assert.Equal(annaId, mine.Id);
        Assert.Equal(2, mine.MessageCount);
        Assert.Equal(80, mine.Preview!.Length);
        Assert.Equal(ewaId, Assert.Single(ewaList).Id);
        Assert.Equal("Pytanie kierownika", ewaList[0].Preview);
    }

    [Fact]
    public async Task UC31_HR_lists_all_and_Open_of_foreign_conversation_writes_ConversationRead_audit()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var anna = c.Store(c.As(c.Org.Anna, Roles.Employee));
        var id = await StartAs(anna);
        await Say(anna, id, StoredRole.User, "Pytanie");
        var hr = c.Store(c.As(c.Org.Piotr, Roles.HR));

        var all = await hr.ListAsync(CancellationToken.None);
        var opened = await hr.OpenAsync(id, CancellationToken.None);

        Assert.Contains(all, x => x.Id == id);
        Assert.Single(opened.Value!);
        var audit = await c.Db.AuditLogs.AsNoTracking().SingleAsync(a => a.EventType == "ConversationRead");
        Assert.Equal(id.ToString(), audit.EntityId);
        Assert.Equal(c.UserIdOf(c.Org.Piotr), audit.ActorUserId);
        await anna.OpenAsync(id, CancellationToken.None);
        Assert.Equal(1, await c.Db.AuditLogs.CountAsync(a => a.EventType == "ConversationRead"));
    }

    [Fact]
    public async Task Q08_Employee_and_Manager_get_NotFound_for_foreign_conversation_in_Open_and_Append_and_Load()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var id = await StartAs(c.Store(c.As(c.Org.Anna, Roles.Employee)));

        foreach (var other in new[] { c.Store(c.As(c.Org.Jan, Roles.Employee)), c.Store(c.As(c.Org.Ewa, Roles.Manager)) })
        {
            Assert.Equal(StoreFailure.NotFound, (await other.OpenAsync(id, CancellationToken.None)).Failure);
            Assert.Equal(StoreFailure.NotFound, (await Say(other, id, StoredRole.User, "wtargnięcie")).Failure);
            Assert.Equal(StoreFailure.NotFound, (await other.LoadAsync(id, 40, CancellationToken.None)).Failure);
        }

        Assert.Equal(0, await c.Db.Messages.CountAsync());
    }

    [Fact]
    public async Task UC31_HR_cannot_append_to_foreign_conversation()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var id = await StartAs(c.Store(c.As(c.Org.Anna, Roles.Employee)));

        var result = await Say(c.Store(c.As(c.Org.Piotr, Roles.HR)), id, StoredRole.User, "nie moja rozmowa");

        Assert.Equal(StoreFailure.NotFound, result.Failure);
        Assert.Equal(0, await c.Db.Messages.CountAsync());
    }

    [Fact]
    public async Task Load_returns_only_last_N_messages_and_starts_at_a_user_message()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var store = c.Store(c.As(c.Org.Anna, Roles.Employee));
        var id = await StartAs(store);
        await Say(store, id, StoredRole.User, "q1");
        await Say(store, id, StoredRole.ToolCall, "t1", "get_leave_balance", "{}");
        await Say(store, id, StoredRole.ToolResult, "{}", "get_leave_balance");
        await Say(store, id, StoredRole.Assistant, "a1");
        await Say(store, id, StoredRole.User, "q2");
        await Say(store, id, StoredRole.Assistant, "a2");

        var last2 = (await store.LoadAsync(id, 2, CancellationToken.None)).Value!;
        var last3 = (await store.LoadAsync(id, 3, CancellationToken.None)).Value!;
        var last5 = (await store.LoadAsync(id, 5, CancellationToken.None)).Value!;

        Assert.Equal(new[] { "q2", "a2" }, last2.Select(m => m.Content));
        Assert.Equal(new[] { "q2", "a2" }, last3.Select(m => m.Content));
        Assert.Equal(new[] { "q2", "a2" }, last5.Select(m => m.Content));
        Assert.Equal(6, (await store.LoadAsync(id, 6, CancellationToken.None)).Value!.Count);
    }

    [Fact]
    public async Task Concurrent_append_conflict_is_Conflict_not_exception()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var user = c.As(c.Org.Anna, Roles.Employee);
        var id = await StartAs(c.Store(user));
        string connection = c.Db.Database.GetConnectionString()!;
        var contexts = new List<Hr.Core.Persistence.HrDbContext>();
        try
        {
            for (int i = 0; i < 8; i++)
            {
                contexts.Add(await fixture.OpenContextAsync(connection, user, c.Clock));
            }

            var results = await Task.WhenAll(contexts.Select((db, i) => Task.Run(() => c.Store(db, user).AppendAsync(id, StoredRole.User, $"m{i}", null, null, CancellationToken.None))));

            var succeeded = results.Where(r => r.IsSuccess).Select(r => r.Value!.Seq).OrderBy(x => x).ToList();
            Assert.NotEmpty(succeeded);
            Assert.All(results.Where(r => !r.IsSuccess), r => Assert.Equal(StoreFailure.Conflict, r.Failure));
            Assert.Equal(succeeded.Count, succeeded.Distinct().Count());
            Assert.Equal(Enumerable.Range(1, succeeded.Count), succeeded);
            Assert.Equal(succeeded.Count, await c.Db.Messages.CountAsync(m => m.ConversationId == id));
        }
        finally
        {
            foreach (var db in contexts)
            {
                await db.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task Append_after_conflict_on_same_store_succeeds_with_next_seq()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var user = c.As(c.Org.Anna, Roles.Employee);
        var store = c.Store(user);
        var id = await StartAs(store);
        await Say(store, id, StoredRole.User, "q1");
        // Wiadomość dopisana poza tym kontekstem (np. równoległe żądanie) zajmuje kolejny numer.
        await using (var other = await fixture.OpenContextAsync(c.Db.Database.GetConnectionString()!, user, c.Clock))
        {
            Assert.True((await Say(c.Store(other, user), id, StoredRole.Assistant, "a1")).IsSuccess);
        }

        var next = await Say(store, id, StoredRole.User, "q2");

        Assert.True(next.IsSuccess);
        Assert.Equal(3, next.Value!.Seq);
    }

    [Fact]
    public async Task RecordWarning_writes_audit_event()
    {
        await using var c = await ServiceTestContext.CreateAsync(fixture);
        var store = c.Store(c.As(c.Org.Anna, Roles.Employee));
        var id = await StartAs(store);

        await store.RecordWarningAsync(id, "AgentUngroundedNumbers", "{\"numbers\":[\"5000\"]}", CancellationToken.None);

        var row = await c.Db.AuditLogs.AsNoTracking().SingleAsync(a => a.EventType == "AgentUngroundedNumbers");
        Assert.Equal("Conversation", row.EntityType);
        Assert.Equal(id.ToString(), row.EntityId);
        Assert.Equal("{\"numbers\":[\"5000\"]}", row.AfterJson);
    }
}

public class StoredRoleTests
{
    [Fact]
    public void StoredRole_matches_domain_MessageRole_names_and_values()
    {
        Assert.Equal(
            Enum.GetValues<Hr.Core.Domain.MessageRole>().Select(r => (r.ToString(), (int)r)),
            Enum.GetValues<StoredRole>().Select(r => (r.ToString(), (int)r)));
    }
}
