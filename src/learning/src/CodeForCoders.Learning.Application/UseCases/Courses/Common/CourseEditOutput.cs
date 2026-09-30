namespace CodeForCoders.Learning.Application.UseCases.Courses.Common;

public sealed record CourseEditOutput(CourseDetailOutput Course, Guid? CreatedId);
