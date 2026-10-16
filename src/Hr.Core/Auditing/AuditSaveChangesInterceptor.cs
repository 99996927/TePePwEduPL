using System.Text.Json;
using System.Text.Json.Serialization;
using Hr.Core.Abstractions;
using Hr.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Hr.Core.Auditing;

/// <summary>Dopisuje wpisy audytu dla zmian encji <see cref="IAuditable"/> w tej samej transakcji co zapis.</summary>
public sealed class AuditSaveChangesInterceptor(ICurrentUser actor, IClock clock) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Audit(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Audit(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Audit(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        context.ChangeTracker.DetectChanges();
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.Entity is IAuditable && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            var (before, after) = entry.State switch
            {
                EntityState.Added => (null, Serialize(entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue))),
                EntityState.Deleted => (Serialize(entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue)), null),
                _ => ModifiedValues(entry),
            };

            context.Add(new AuditLog
            {
                ActorUserId = actor.UserId,
                AtUtc = clock.UtcNow,
                EventType = entry.State.ToString(),
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = entry.Properties.First(p => p.Metadata.IsPrimaryKey()).CurrentValue?.ToString() ?? string.Empty,
                BeforeJson = before,
                AfterJson = after,
            });
        }
    }

    private static (string? Before, string? After) ModifiedValues(EntityEntry entry)
    {
        var changed = entry.Properties.Where(p => p.IsModified).ToList();
        return (
            Serialize(changed.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue)),
            Serialize(changed.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue)));
    }

    private static string Serialize(Dictionary<string, object?> values) => JsonSerializer.Serialize(values, JsonOptions);
}
