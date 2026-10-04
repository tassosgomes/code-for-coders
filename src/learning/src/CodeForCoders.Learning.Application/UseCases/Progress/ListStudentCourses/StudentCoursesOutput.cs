namespace CodeForCoders.Learning.Application.UseCases.Progress.ListStudentCourses;

public sealed record StudentCoursesOutput(bool ProgressAvailable, IReadOnlyList<ActiveStudentCourse> Active, IReadOnlyList<EndedStudentCourse> Ended);
