using CodeForCoders.Learning.Domain.Entities;

namespace CodeForCoders.Learning.Domain.SeedWork;

public sealed class CourseIncompleteException(IReadOnlyList<PublicationPendency> pendencies)
    : Exception("The course curriculum is incomplete.")
{
    public IReadOnlyList<PublicationPendency> Pendencies { get; } = pendencies;
}
