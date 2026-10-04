using CodeForCoders.Learning.Application.Interfaces;

namespace CodeForCoders.Learning.Application.UseCases.Progress.GetStudentCourseProgress;

public interface IGetStudentCourseProgress : IUseCase<GetStudentCourseProgressInput, StudentCourseProgress>;
