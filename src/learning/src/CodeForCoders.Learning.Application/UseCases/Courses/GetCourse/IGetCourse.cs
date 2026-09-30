using CodeForCoders.Learning.Application.UseCases.Courses.Common;

namespace CodeForCoders.Learning.Application.UseCases.Courses.GetCourse;

public interface IGetCourse : IUseCase<Guid, CourseDetailOutput>;
