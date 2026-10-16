using Hr.Core.Abstractions;
using Hr.Core.Domain;

namespace Hr.Core.Security;

/// <summary>Dostęp do rozmów z agentem (ADR-11): autor widzi swoje, kadry wszystkie, pozostali tylko własne.</summary>
public static class ConversationAccess
{
    public static IQueryable<Conversation> Visible(this IQueryable<Conversation> source, ICurrentUser user)
    {
        if (user.UserId is not { } userId)
        {
            return source.Where(_ => false);
        }

        if (user.IsInRole(Roles.HR) || user.IsInRole(Roles.HRApprover))
        {
            return source;
        }

        return source.Where(c => c.UserId == userId);
    }
}
