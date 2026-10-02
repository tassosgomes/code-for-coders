namespace CodeForCoders.Commerce.Application.UseCases.StudentAccessGrants.ListStudentAccessGrants;

public sealed record ListStudentAccessGrantsInput(Guid StudentId, int Page, int Size);
