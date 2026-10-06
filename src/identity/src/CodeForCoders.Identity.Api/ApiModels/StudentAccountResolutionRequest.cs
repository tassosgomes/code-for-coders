namespace CodeForCoders.Identity.Api.ApiModels;

public sealed record StudentAccountResolutionRequest(IReadOnlyList<Guid>? StudentIds);
