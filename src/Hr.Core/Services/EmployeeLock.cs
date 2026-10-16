using Hr.Core.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Hr.Core.Services;

/// <summary>
/// Serializuje operacje „sprawdź i zapisz” jednego pracownika (np. dwuklik na „Złóż wniosek”):
/// transakcja z blokadą aplikacyjną SQL Server (sp_getapplock) trzymaną do jej końca.
/// </summary>
internal static class EmployeeLock
{
    internal static async Task<IDbContextTransaction> BeginAsync(HrDbContext db, string scope, Guid employeeId, CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(ct);
        var resource = $"{scope}:{employeeId}";
        var status = (await db.Database
            .SqlQuery<int>($"DECLARE @r int; EXEC @r = sp_getapplock @Resource = {resource}, @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 15000; SELECT @r AS [Value]")
            .ToListAsync(ct)).Single();
        if (status < 0)
        {
            await transaction.DisposeAsync();
            throw new TimeoutException($"Nie udało się uzyskać blokady operacji '{scope}' dla pracownika {employeeId} (kod {status}).");
        }

        return transaction;
    }
}
