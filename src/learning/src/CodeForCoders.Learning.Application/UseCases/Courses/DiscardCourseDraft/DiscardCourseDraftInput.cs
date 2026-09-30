using CodeForCoders.Learning.Application.UseCases.Courses.Common;

namespace CodeForCoders.Learning.Application.UseCases.Courses.DiscardCourseDraft;

public sealed record DiscardCourseDraftInput(CourseWriteContext Context, int DraftRevision);
