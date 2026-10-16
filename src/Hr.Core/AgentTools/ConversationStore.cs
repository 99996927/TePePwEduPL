using Hr.Core.Abstractions;
using Hr.Core.Auditing;
using Hr.Core.Domain;
using Hr.Core.Persistence;
using Hr.Core.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.AgentTools;

public sealed class ConversationStore(HrDbContext db, ICurrentUser user, IClock clock, ConversationReader reader, IAuditWriter audit) : IConversationStore
{
    private const int PreviewLength = 80;

    public async Task<StoreResult<Guid>> StartAsync(CancellationToken ct)
    {
        if (user.UserId is not { } userId)
        {
            return StoreResult<Guid>.Fail(StoreFailure.NotFound);
        }

        var conversation = new Conversation { Id = Guid.NewGuid(), UserId = userId, StartedAtUtc = clock.UtcNow };
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync(ct);
        return StoreResult<Guid>.Ok(conversation.Id);
    }

    public async Task<StoreResult<IReadOnlyList<StoredMessage>>> LoadAsync(Guid conversationId, int lastN, CancellationToken ct)
    {
        if (!await OwnsAsync(conversationId, ct))
        {
            return StoreResult<IReadOnlyList<StoredMessage>>.Fail(StoreFailure.NotFound);
        }

        var recent = await db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.Seq)
            .Take(Math.Max(lastN, 0))
            .ToListAsync(ct);
        recent.Reverse();
        int firstUser = recent.FindIndex(m => m.Role == MessageRole.User);
        IReadOnlyList<StoredMessage> messages = firstUser < 0 ? [] : recent.Skip(firstUser).Select(Map).ToList();
        return StoreResult<IReadOnlyList<StoredMessage>>.Ok(messages);
    }

    public async Task<StoreResult<StoredMessage>> AppendAsync(
        Guid conversationId, MessageRole role, string content, string? toolName, string? toolParamsJson, CancellationToken ct)
    {
        if (!await OwnsAsync(conversationId, ct))
        {
            return StoreResult<StoredMessage>.Fail(StoreFailure.NotFound);
        }

        int last = await db.Messages.Where(m => m.ConversationId == conversationId).Select(m => (int?)m.Seq).MaxAsync(ct) ?? 0;
        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Seq = last + 1,
            Role = role,
            Content = content,
            ToolName = toolName,
            ToolParamsJson = toolParamsJson,
            CreatedAtUtc = clock.UtcNow,
        };
        db.Messages.Add(message);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.Entry(message).State = EntityState.Detached;
            return StoreResult<StoredMessage>.Fail(StoreFailure.Conflict);
        }

        return StoreResult<StoredMessage>.Ok(Map(message));
    }

    public async Task<IReadOnlyList<ConversationSummary>> ListAsync(CancellationToken ct)
    {
        var rows = await db.Conversations.Visible(user)
            .OrderByDescending(c => c.StartedAtUtc)
            .Select(c => new
            {
                c.Id,
                c.UserId,
                c.StartedAtUtc,
                First = db.Messages.Where(m => m.ConversationId == c.Id && m.Role == MessageRole.User)
                    .OrderBy(m => m.Seq).Select(m => m.Content).FirstOrDefault(),
                Count = db.Messages.Count(m => m.ConversationId == c.Id),
            })
            .AsNoTracking()
            .ToListAsync(ct);
        return rows
            .Select(r => new ConversationSummary(
                r.Id, r.UserId, DateTime.SpecifyKind(r.StartedAtUtc, DateTimeKind.Utc),
                r.First is null ? null : r.First.Length <= PreviewLength ? r.First : r.First[..PreviewLength], r.Count))
            .ToList();
    }

    public async Task<StoreResult<IReadOnlyList<StoredMessage>>> OpenAsync(Guid conversationId, CancellationToken ct)
    {
        var conversation = await reader.OpenAsync(conversationId, ct);
        return conversation is null
            ? StoreResult<IReadOnlyList<StoredMessage>>.Fail(StoreFailure.NotFound)
            : StoreResult<IReadOnlyList<StoredMessage>>.Ok(conversation.Messages.Select(Map).ToList());
    }

    public async Task RecordWarningAsync(Guid conversationId, string eventType, string detailsJson, CancellationToken ct)
    {
        audit.Record(eventType, nameof(Conversation), conversationId.ToString(), detailsJson);
        await db.SaveChangesAsync(ct);
    }

    private async Task<bool> OwnsAsync(Guid conversationId, CancellationToken ct) =>
        user.UserId is { } userId && await db.Conversations.AnyAsync(c => c.Id == conversationId && c.UserId == userId, ct);

    private static StoredMessage Map(Message m) =>
        new(m.Id, m.Seq, m.Role, m.Content, m.ToolName, m.ToolParamsJson, DateTime.SpecifyKind(m.CreatedAtUtc, DateTimeKind.Utc));
}
