using CodeForCoders.BffStudent.Application.Common;
using CodeForCoders.BffStudent.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CodeForCoders.BffStudent.Infra.Data;

public sealed class BffStudentDbContextFactory : IDesignTimeDbContextFactory<BffStudentDbContext>
{
    public BffStudentDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=code_for_coders_bff_student;Username=code_for_coders_bff_student";
        var options = new DbContextOptionsBuilder<BffStudentDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                "__ef_migrations_history",
                BffStudentSchema.Name))
            .Options;

        return new BffStudentDbContext(options, new TenantContext());
    }
}
