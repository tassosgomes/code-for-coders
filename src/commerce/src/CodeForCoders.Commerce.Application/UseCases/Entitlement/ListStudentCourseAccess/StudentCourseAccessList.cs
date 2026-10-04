using CodeForCoders.Commerce.Application.Interfaces;

namespace CodeForCoders.Commerce.Application.UseCases.Entitlement.ListStudentCourseAccess;

public sealed record StudentCourseAccessList(IReadOnlyList<StudentCourseAccess> Data);
