using CodeForCoders.Learning.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Learning.Infra.Data.Catalog;

public sealed class CatalogInitialLoadExecutionConfiguration : IEntityTypeConfiguration<CatalogInitialLoadExecution>
{
    public void Configure(EntityTypeBuilder<CatalogInitialLoadExecution> builder)
    {
        builder.ToTable("catalog_initial_load_executions", LearningSchemas.Content);
        builder.HasKey(execution => execution.Name);
        builder.Property(execution => execution.Name).HasColumnName("name").HasMaxLength(100).ValueGeneratedNever();
        builder.Property(execution => execution.CompletedAt).HasColumnName("completed_at").IsRequired();
        builder.Property(execution => execution.CourseCount).HasColumnName("course_count").IsRequired();
    }
}
