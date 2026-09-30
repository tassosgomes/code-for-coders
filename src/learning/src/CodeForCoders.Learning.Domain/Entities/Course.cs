using CodeForCoders.Learning.Domain.SeedWork;

namespace CodeForCoders.Learning.Domain.Entities;

public sealed class Course
{
    private Course() { }

    public List<CourseModule> Modules { get; private set; } = [];

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

    public void Update(CourseChanges changes)
    {
        ValidateChanges(changes);
        if (changes.Title is not null) Title = changes.Title.Trim();
        if (changes.HasDescription) Description = changes.Description;
    }

    public Guid AddModule(CourseChanges changes)
    {
        ValidateTitle(changes.Title);
        if (Modules.Count >= 100) throw new CourseRuleException("STRUCTURE_LIMIT_REACHED");
        var position = ValidatePosition(changes.Position ?? Modules.Count + 1, Modules.Count + 1);
        var module = CourseModule.Create(Id, changes.Title!);
        Modules.Insert(position - 1, module);
        RenumberModules();
        return module.Id;
    }

    public void UpdateModule(Guid moduleId, CourseChanges changes)
    {
        var module = FindModule(moduleId);
        ValidateChanges(changes);
        var position = ValidatePosition(changes.Position ?? module.Position, Modules.Count);
        if (changes.Title is not null) module.Rename(changes.Title);
        Modules.Remove(module);
        Modules.Insert(position - 1, module);
        RenumberModules();
    }

    public void RemoveModule(Guid moduleId)
    {
        Modules.Remove(FindModule(moduleId));
        RenumberModules();
    }

    public Guid AddLesson(Guid moduleId, CourseChanges changes)
    {
        var module = FindModule(moduleId);
        ValidateTitle(changes.Title);
        ValidateChanges(changes);
        if (module.Lessons.Count >= 200) throw new CourseRuleException("STRUCTURE_LIMIT_REACHED");
        var position = ValidatePosition(changes.Position ?? module.Lessons.Count + 1, module.Lessons.Count + 1);
        var lesson = CourseLesson.Create(module.Id, changes);
        module.Lessons.Insert(position - 1, lesson);
        module.RenumberLessons();
        return lesson.Id;
    }

    public void UpdateLesson(Guid lessonId, CourseChanges changes)
    {
        var source = Modules.SingleOrDefault(module => module.Lessons.Any(lesson => lesson.Id == lessonId))
            ?? throw new CourseItemNotFoundException("LESSON_NOT_FOUND");
        var lesson = source.Lessons.Single(lesson => lesson.Id == lessonId);
        var destination = changes.ModuleId.HasValue ? FindModule(changes.ModuleId.Value) : source;
        ValidateChanges(changes);
        if (destination != source && destination.Lessons.Count >= 200) throw new CourseRuleException("STRUCTURE_LIMIT_REACHED");
        var count = destination.Lessons.Count + (destination == source ? 0 : 1);
        var position = ValidatePosition(changes.Position ?? (destination == source ? lesson.Position : count), count);
        lesson.Update(changes);
        source.Lessons.Remove(lesson);
        destination.Lessons.Insert(position - 1, lesson);
        lesson.MoveTo(destination.Id);
        source.RenumberLessons();
        destination.RenumberLessons();
    }

    public void RemoveLesson(Guid lessonId)
    {
        var module = Modules.SingleOrDefault(item => item.Lessons.Any(lesson => lesson.Id == lessonId))
            ?? throw new CourseItemNotFoundException("LESSON_NOT_FOUND");
        module.Lessons.RemoveAll(lesson => lesson.Id == lessonId);
        module.RenumberLessons();
    }

    public void RecordEdit(CourseCreation actor)
    {
        LastEditedById = actor.ActorId;
        LastEditedByName = actor.ActorName;
        LastEditedAt = actor.Now;
        DraftRevision++;
        HasUnpublishedChanges = CurrentVersion.HasValue;
    }

    private CourseModule FindModule(Guid id) => Modules.SingleOrDefault(module => module.Id == id)
        ?? throw new CourseItemNotFoundException("MODULE_NOT_FOUND");

    private static int ValidatePosition(int position, int count)
    {
        if (position < 1 || position > count) throw new CourseRuleException("INVALID_POSITION");
        return position;
    }

    private static void ValidateTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new CourseRuleException("TITLE_REQUIRED");
        if (title.Length > 200) throw new CourseRuleException("INVALID_REQUEST");
    }

    private static void ValidateChanges(CourseChanges changes)
    {
        if (changes.Title is not null) ValidateTitle(changes.Title);
        if (changes.Description?.Length > 5000) throw new CourseRuleException("INVALID_REQUEST");
    }

    private void RenumberModules()
    {
        for (var index = 0; index < Modules.Count; index++) Modules[index].SetPosition(index + 1);
    }
}
