using CodeForCoders.Learning.Application.Common;
using CodeForCoders.Learning.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;
using Xunit;

namespace CodeForCoders.Learning.IntegrationTests;

public sealed class CoursePrerequisiteLegacyFixture : IAsyncDisposable
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:18").Build();

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await database.StartAsync(cancellationToken);
        await using var context = new LearningDbContext(new DbContextOptionsBuilder<LearningDbContext>().UseNpgsql(database.GetConnectionString()).Options, new TenantContext());
        await context.GetService<IMigrator>().MigrateAsync("20260930174156_AddCourseLevel", cancellationToken);
        var tenant = Guid.CreateVersion7(); var actor = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow; var fingerprint = new string('A', 64);
        foreach (var hasChanges in new[] { false, true })
        {
            var id = Guid.CreateVersion7();
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO content.courses
                    (id, tenant_id, title, draft_revision, current_version, has_unpublished_changes, published_fingerprint,
                     created_by_id, created_by_name, created_at, last_edited_by_id, last_edited_by_name, last_edited_at)
                VALUES ({id}, {tenant}, 'AÇÃO À Ê Í Ó Ú Ü', 1, 1, {hasChanges}, {fingerprint}, {actor}, 'Teacher', {now}, {actor}, 'Teacher', {now})
                """, cancellationToken);
        }
        await context.Database.MigrateAsync(cancellationToken);
        var courses = await context.Courses.IgnoreQueryFilters().ToListAsync(cancellationToken);
        Assert.Equal(2, courses.Count);
        Assert.All(courses, course => { Assert.Equal("acao a e i o u u", course.TitleSearch); Assert.Null(course.PrerequisiteText); Assert.Empty(course.RecommendedCourseIds); Assert.Null(course.PublishedFingerprint); });
        Assert.Single(courses, course => course.HasUnpublishedChanges); Assert.Single(courses, course => !course.HasUnpublishedChanges);
    }

    public ValueTask DisposeAsync() => database.DisposeAsync();
}
