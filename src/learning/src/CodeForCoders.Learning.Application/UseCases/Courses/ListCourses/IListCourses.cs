using CodeForCoders.Learning.Application.Interfaces;

namespace CodeForCoders.Learning.Application.UseCases.Courses.ListCourses;

public interface IListCourses : IUseCase<CourseListQuery, CoursePage>;
