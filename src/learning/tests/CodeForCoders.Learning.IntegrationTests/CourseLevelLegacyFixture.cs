using CodeForCoders.Learning.Application.Common;
using CodeForCoders.Learning.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;
using Xunit;

namespace CodeForCoders.Learning.IntegrationTests;

public sealed class CourseLevelLegacyFixture : IAsyncDisposable
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:18").Build();
    public string ConnectionString => database.GetConnectionString();
    public Guid TenantId { get; } = Guid.CreateVersion7();
    public Guid CourseId { get; } = Guid.CreateVersion7();

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await database.StartAsync(cancellationToken);
        await using var context = new LearningDbContext(new DbContextOptionsBuilder<LearningDbContext>().UseNpgsql(ConnectionString).Options, new TenantContext());
        await context.GetService<IMigrator>().MigrateAsync("20260930025636_AddPublishedContentFingerprint", cancellationToken);
        var actor = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow; var oldHash = new string('A', 64);
        foreach (var hasChanges in new[] { false, true })
        {
            var id = hasChanges ? Guid.CreateVersion7() : CourseId;
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO content.courses
                    (id, tenant_id, title, description, draft_revision, current_version, has_unpublished_changes, published_fingerprint,
                     created_by_id, created_by_name, created_at, last_edited_by_id, last_edited_by_name, last_edited_at)
                VALUES ({id}, {TenantId}, 'Legacy course', NULL, 1, 1, {hasChanges}, {oldHash}, {actor}, 'Teacher', {now}, {actor}, 'Teacher', {now})
                """, cancellationToken);
        }
        var versionId = Guid.CreateVersion7();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO content.course_versions
                (id, tenant_id, course_id, version_number, title, description, version_note, published_by_id, published_by_name, published_at, modules)
            VALUES ({versionId}, {TenantId}, {CourseId}, 1, 'Legacy course', NULL, NULL, {actor}, 'Teacher', {now}, '[]'::jsonb)
            """, cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
        var courses = await context.Courses.IgnoreQueryFilters().ToListAsync(cancellationToken);
        Assert.All(courses, course => { Assert.Null(course.Level); Assert.Null(course.CurrentLevel); Assert.Null(course.PublishedFingerprint); });
        Assert.False(courses.Single(course => course.Id == CourseId).HasUnpublishedChanges);
        Assert.True(courses.Single(course => course.Id != CourseId).HasUnpublishedChanges);
    }

    public ValueTask DisposeAsync() => database.DisposeAsync();
}
