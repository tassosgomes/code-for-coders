using CodeForCoders.Learning.Application.Interfaces;

namespace CodeForCoders.Learning.Application.UseCases.StudentLessons.GetStudentLesson;

public interface IGetStudentLesson : IUseCase<GetStudentLessonInput, StudentLessonScreen>;
