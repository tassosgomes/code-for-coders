using System.Text.Json;

namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record CatalogCourseDetail(Guid CourseId, string Title, string? Level,
    CatalogPrerequisite Prerequisite, string? Tagline, bool InShowcase, IReadOnlyList<JsonElement> Offers);
