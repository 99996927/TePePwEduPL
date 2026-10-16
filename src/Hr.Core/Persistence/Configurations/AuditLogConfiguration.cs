using Hr.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hr.Core.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public const string AppendOnlyTrigger = "TR_AuditLog_AppendOnly";

    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        // HasTrigger: bez tego EF używa OUTPUT bez INTO, czego SQL Server nie dopuszcza dla tabel z wyzwalaczami.
        builder.ToTable("AuditLog", t => t.HasTrigger(AppendOnlyTrigger));
        builder.Property(a => a.EventType).HasMaxLength(50).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(100).IsRequired();
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.HasIndex(a => a.AtUtc);
    }
}
