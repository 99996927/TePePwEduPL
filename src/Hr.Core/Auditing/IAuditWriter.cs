namespace Hr.Core.Auditing;

public interface IAuditWriter
{
    /// <summary>Dodaje wpis audytu do bieżącego kontekstu; zostanie zapisany przy najbliższym <c>SaveChanges</c>.</summary>
    void Record(string eventType, string entityType, string entityId, string? afterJson = null);
}
