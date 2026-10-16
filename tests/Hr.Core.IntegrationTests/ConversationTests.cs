using Hr.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.IntegrationTests;

[Collection("mssql")]
public class ConversationTests(MsSqlFixture fixture)
{
    private static readonly DateTime Now = new(2026, 10, 16, 9, 0, 0, DateTimeKind.Utc);

    private static Message Msg(Guid conversationId, int seq, MessageRole role, string content, string? tool = null, string? json = null) => new()
    {
        Id = Guid.NewGuid(), ConversationId = conversationId, Seq = seq, Role = role,
        Content = content, ToolName = tool, ToolParamsJson = json, CreatedAtUtc = Now,
    };

    [Fact]
    public async Task Conversation_with_tool_call_roundtrips()
    {
        await using var db = await fixture.NewContextAsync();
        var conversation = new Conversation { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), StartedAtUtc = Now };
        db.Conversations.Add(conversation);
        db.Messages.Add(Msg(conversation.Id, 3, MessageRole.Assistant, "Masz 2 h nadgodzin."));
        db.Messages.Add(Msg(conversation.Id, 1, MessageRole.User, "Ile mam nadgodzin?"));
        db.Messages.Add(Msg(conversation.Id, 2, MessageRole.ToolCall, "", "calculate_overtime", "{\"month\":\"2026-10\"}"));
        await db.SaveChangesAsync();

        var messages = await db.Messages.AsNoTracking().Where(m => m.ConversationId == conversation.Id).OrderBy(m => m.Seq).ToListAsync();
        Assert.Equal(new[] { 1, 2, 3 }, messages.Select(m => m.Seq));
        Assert.Equal("calculate_overtime", messages[1].ToolName);
        Assert.Equal("{\"month\":\"2026-10\"}", messages[1].ToolParamsJson);
    }

    [Fact]
    public async Task Duplicate_seq_in_conversation_is_rejected()
    {
        await using var db = await fixture.NewContextAsync();
        var conversation = new Conversation { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), StartedAtUtc = Now };
        db.Conversations.Add(conversation);
        db.Messages.Add(Msg(conversation.Id, 1, MessageRole.User, "a"));
        db.Messages.Add(Msg(conversation.Id, 1, MessageRole.Assistant, "b"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Role_is_stored_as_text()
    {
        await using var db = await fixture.NewContextAsync();
        var conversation = new Conversation { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), StartedAtUtc = Now };
        db.Conversations.Add(conversation);
        db.Messages.Add(Msg(conversation.Id, 1, MessageRole.ToolCall, "", "get_leave_balance", "{}"));
        await db.SaveChangesAsync();

        var role = await db.Database.SqlQueryRaw<string>("SELECT CAST(Role AS nvarchar(30)) AS [Value] FROM Messages").SingleAsync();
        Assert.Equal("ToolCall", role);
    }
}
