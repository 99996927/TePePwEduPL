using Hr.Core.Abstractions;
using Hr.Core.Auditing;
using Hr.Core.Domain;
using Hr.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hr.Core.Security;

public sealed class ConversationReader(HrDbContext db, IAuditWriter audit, ICurrentUser user)
{
    /// <summary>Zwraca rozmowę z wiadomościami w kolejności <c>Seq</c> albo <c>null</c>, gdy użytkownik jej nie widzi.
    /// Odczyt cudzej rozmowy (UC-31) jest zapisywany w audycie w tym samym <c>SaveChanges</c>.</summary>
    public async Task<Conversation?> OpenAsync(Guid conversationId, CancellationToken ct = default)
    {
        var conversation = await db.Conversations
            .Visible(user)
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct);
        if (conversation is null)
        {
            return null;
        }

        conversation.Messages = conversation.Messages.OrderBy(m => m.Seq).ToList();
        if (conversation.UserId != user.UserId)
        {
            audit.Record("ConversationRead", nameof(Conversation), conversation.Id.ToString());
            await db.SaveChangesAsync(ct);
        }

        return conversation;
    }
}
