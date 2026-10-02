namespace CodeForCoders.BffAdmin.Application.Interfaces;

public sealed record CourtesyCoursesRequest(string AccessToken, int Page, int Size, string? Title);
