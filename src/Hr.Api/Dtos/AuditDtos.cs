using Hr.Core.Domain;
using Hr.Core.Services;

namespace Hr.Api.Dtos;

public sealed record AuditEntryDto(
    long Id, Guid? ActorUserId, DateTime AtUtc, string EventType, string EntityType, string EntityId, string? BeforeJson, string? AfterJson)
{
    public static AuditEntryDto Of(AuditLog a) =>
        new(a.Id, a.ActorUserId, a.AtUtc.AsUtc(), a.EventType, a.EntityType, a.EntityId, a.BeforeJson, a.AfterJson);
}

public sealed record AuditPageDto(int Total, IReadOnlyList<AuditEntryDto> Items)
{
    public static AuditPageDto Of(AuditPage p) => new(p.Total, p.Items.Select(AuditEntryDto.Of).ToList());
}
