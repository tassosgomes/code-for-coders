using System.Text.Json;

namespace CodeForCoders.Commerce.Application.UseCases.CatalogCourses.UpdateCatalogCourse;

public sealed record UpdateCatalogCourseInput(Guid TenantId, Guid ActorId, Guid CourseId, string IdempotencyKey, JsonElement Body);
