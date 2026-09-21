using CodeForCoders.Audit.Domain.Entities;
using CodeForCoders.Audit.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodeForCoders.Audit.UnitTests;

public sealed class AuditAppendOnlyGuardrailTests
{
    [Fact]
    public void SaveChangesRejectsModifiedAuditRecordBeforeOpeningDatabase()
    {
        using var context = CreateContext();
        var record = NewRecord();
        context.Attach(record);
        context.Entry(record).State = EntityState.Modified;

        var exception = Assert.Throws<InvalidOperationException>(() => context.SaveChanges());

        Assert.Equal(AuditAppendOnlyPolicy.MutationRejectedMessage, exception.Message);
    }

    [Fact]
    public void SaveChangesRejectsDeletedAuditRecordBeforeOpeningDatabase()
    {
        using var context = CreateContext();
        var record = NewRecord();
        context.Attach(record);
        context.Entry(record).State = EntityState.Deleted;

        var exception = Assert.Throws<InvalidOperationException>(() => context.SaveChanges());

        Assert.Equal(AuditAppendOnlyPolicy.MutationRejectedMessage, exception.Message);
    }

    private static AuditDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql("Host=localhost;Database=code_for_coders_audit;Username=code_for_coders_audit")
            .Options;
        return new AuditDbContext(options);
    }

    private static AuditRecord NewRecord()
        => AuditRecord.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "smoke-source",
            "SmokeEventV1",
            "{}",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
}
