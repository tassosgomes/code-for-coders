using CodeForCoders.Learning.Domain.SeedWork;

namespace CodeForCoders.Learning.Domain.Entities;

public sealed class Course
{
    private Course() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int DraftRevision { get; private set; }
    public int? CurrentVersion { get; private set; }
    public bool HasUnpublishedChanges { get; private set; }
    public Guid CreatedById { get; private set; }
    public string CreatedByName { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid LastEditedById { get; private set; }
    public string LastEditedByName { get; private set; } = string.Empty;
    public DateTimeOffset LastEditedAt { get; private set; }

    public static Course Create(CourseCreation input)
    {
        if (input.TenantId == Guid.Empty || input.ActorId == Guid.Empty || string.IsNullOrWhiteSpace(input.ActorName))
            throw new CourseRuleException("INVALID_REQUEST");
        if (string.IsNullOrWhiteSpace(input.Title)) throw new CourseRuleException("TITLE_REQUIRED");
        if (input.Title.Length > 200 || input.Description?.Length > 5000)
            throw new CourseRuleException("INVALID_REQUEST");
        return new Course
        {
            Id = Guid.CreateVersion7(input.Now),
            TenantId = input.TenantId,
            Title = input.Title.Trim(),
            Description = input.Description,
            DraftRevision = 1,
            CreatedById = input.ActorId,
            CreatedByName = input.ActorName,
            LastEditedById = input.ActorId,
            LastEditedByName = input.ActorName,
            CreatedAt = input.Now,
            LastEditedAt = input.Now,
        };
    }
}
