using CodeForCoders.Learning.Application.Interfaces;

namespace CodeForCoders.Learning.Application.UseCases.Courses.ListCourseVersions;

public interface IListCourseVersions : IUseCase<ListCourseVersionsInput, CourseVersionSummaryPage>;
