using System.Net;
using CodeForCoders.Learning.Application.Common;
using CodeForCoders.Learning.Domain.Entities;
using CodeForCoders.Learning.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodeForCoders.Learning.IntegrationTests;

public sealed class CourseProgressReadTestFixture(StudentLessonApiFactory factory)
{
    public Guid StudentId { get; } = Guid.CreateVersion7();
    public Course Course { get; private set; } = null!;
    public CourseVersion Version { get; private set; } = null!;
    public IReadOnlyList<PublishedLesson> Lessons => Version.Modules.SelectMany(module => module.Lessons).ToArray();
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    public LearningDbContext Context()
    {
        var tenant = new TenantContext(); tenant.Set(Course.TenantId);
        return new(new DbContextOptionsBuilder<LearningDbContext>().UseNpgsql(factory.DatabaseConnection).Options, tenant);
    }
    public async Task SeedAsync(int total = 8, int completed = 0, bool published = true)
    {
        var actor = new CourseCreation(Guid.CreateVersion7(), Guid.CreateVersion7(), "Teacher", "Private course", null, DateTimeOffset.UtcNow);
        Course = Course.Create(actor);
        var module = Course.AddModule(new("Private module", null, false, null, null));
        for (var index = 0; index < total; index++)
            Course.AddLesson(module, new($"Private lesson {index}", null, false, null, null, Guid.CreateVersion7(), true));
        await using var db = Context(); db.Courses.Add(Course);
        if (published) { Version = Course.Publish(new(Course.DraftRevision, null, actor)); db.CourseVersions.Add(Version); }
        await db.SaveChangesAsync(Cancellation);
        factory.Commerce.Status = HttpStatusCode.OK; factory.Commerce.Timeout = false; factory.Commerce.PermittedCourseId = null;
        factory.Commerce.Decision = new("allowed", new("until", DateTimeOffset.UtcNow.AddMonths(6)), null, null, DateTimeOffset.UtcNow);
        for (var index = 0; index < completed; index++) await ProgressAsync(Lessons[index].LessonId, 252, completed: true);
    }
    public async Task ProgressAsync(Guid lessonId, int position, string reason = "paused", bool completed = false, Guid? student = null)
    {
        await using var db = Context(); var now = DateTimeOffset.UtcNow; DateTimeOffset? ended = completed ? now : null;
        var studentId = student ?? StudentId;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO progress.lesson_progress (tenant_id, student_id, lesson_id, course_id, last_position_seconds,
                occurred_at, sequence, reason, max_position_seconds, completed_at, last_activity_at)
            VALUES ({Course.TenantId}, {studentId}, {lessonId}, {Course.Id}, {position}, {now}, 1, {reason}, {position}, {ended}, {now})
            """, Cancellation);
    }
    public async Task DurationAsync(Guid videoId, int duration)
    {
        await using var db = Context();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO progress.video_durations (tenant_id, video_id, duration_seconds, occurred_at, event_id)
            VALUES ({Course.TenantId}, {videoId}, {duration}, {DateTimeOffset.UtcNow}, {Guid.CreateVersion7()})
            """, Cancellation);
    }
    public async Task RepublishAsync(Action<Course> change)
    {
        await using var db = Context();
        var tracked = await db.Courses.Include(course => course.Modules).ThenInclude(module => module.Lessons)
            .SingleAsync(course => course.Id == Course.Id, Cancellation);
        change(tracked);
        Version = tracked.Publish(new(tracked.DraftRevision, null, new(Course.TenantId, Course.CreatedById, "Teacher", Course.Title, null, DateTimeOffset.UtcNow)));
        db.CourseVersions.Add(Version); await db.SaveChangesAsync(Cancellation);
    }
    public HttpClient Client() => factory.Student(Course.TenantId, StudentId);
    public Task<HttpResponseMessage> GetAsync(HttpClient client, Guid? courseId = null)
        => client.GetAsync($"/internal/v1/student-courses/{courseId ?? Course.Id}/progress?studentId={Guid.CreateVersion7()}", Cancellation);
}
