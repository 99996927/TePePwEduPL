using Hr.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hr.Core.Persistence.Configurations;

public sealed class LabourCodeArticleConfiguration : IEntityTypeConfiguration<LabourCodeArticle>
{
    public void Configure(EntityTypeBuilder<LabourCodeArticle> builder)
    {
        builder.ToTable("LabourCodeArticles");
        builder.HasKey(a => a.Number);
        builder.Property(a => a.Number).HasMaxLength(20);
        builder.Property(a => a.Text).IsRequired();
    }
}
