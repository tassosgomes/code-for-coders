namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed record ResolvedStudentAccount(Guid StudentId, string Name, string Email, string Status);
