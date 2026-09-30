using CodeForCoders.Learning.Application.UseCases.Courses.Common;

namespace CodeForCoders.Learning.Application.UseCases.Courses.CreateCourse;

public interface ICreateCourse : IUseCase<CreateCourseInput, CourseDetailOutput>;
