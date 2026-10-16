using System.Runtime.CompilerServices;
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
    // Wpisy dodane przez interceptor w bieżącej próbie zapisu; po nieudanym zapisie muszą zniknąć z kontekstu,
    // inaczej ponowny SaveChanges zapisałby je razem z nowymi (wpisy-widma).
    private readonly ConditionalWeakTable<DbContext, List<AuditLog>> _pending = new();

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

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Forget(eventData.Context);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Forget(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        Discard(eventData.Context);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Discard(eventData.Context);
        return Task.CompletedTask;
    }

    private void Forget(DbContext? context)
    {
        if (context is not null)
        {
            _pending.Remove(context);
        }
    }

    private void Discard(DbContext? context)
    {
        if (context is not null && _pending.TryGetValue(context, out var created))
        {
            foreach (var entry in created)
            {
                context.Entry(entry).State = EntityState.Detached;
            }

            _pending.Remove(context);
        }
    }

    private void Audit(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        Discard(context);
        var createdNow = _pending.GetOrCreateValue(context);
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

            var audit = new AuditLog
            {
                ActorUserId = actor.UserId,
                AtUtc = clock.UtcNow,
                EventType = entry.State.ToString(),
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = entry.Properties.First(p => p.Metadata.IsPrimaryKey()).CurrentValue?.ToString() ?? string.Empty,
                BeforeJson = before,
                AfterJson = after,
            };
            createdNow.Add(audit);
            context.Add(audit);
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
