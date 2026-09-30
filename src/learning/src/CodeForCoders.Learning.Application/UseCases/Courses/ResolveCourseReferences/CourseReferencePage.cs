using CodeForCoders.Learning.Application.Interfaces;

namespace CodeForCoders.Learning.Application.UseCases.Courses.ResolveCourseReferences;

public sealed record CourseReferencePage(IReadOnlyList<CourseReference> Data);
