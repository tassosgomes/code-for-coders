using CodeForCoders.Commerce.Application.Interfaces;

namespace CodeForCoders.Commerce.Application.UseCases.CatalogCourses.GetCatalogCourse;

public interface IGetCatalogCourse : IUseCase<Guid, CatalogCourseDetail>;
