using System.Security.Cryptography;
using System.Text;
using CodeForCoders.Learning.Application.Exceptions;
using CodeForCoders.Learning.Application.Interfaces;
using CodeForCoders.Learning.Application.UseCases.Courses.Common;
using CodeForCoders.Learning.Domain.Entities;
using CodeForCoders.Learning.Domain.Repositories;
using CodeForCoders.Learning.Domain.SeedWork;
using FluentValidation;

namespace CodeForCoders.Learning.Application.UseCases.Courses.DeleteCourse;

public sealed class DeleteCourse(
    ICourseRepository courses, ICourseEditStore edits, ICourseVersionStore versions,
    IUnitOfWork unitOfWork, IValidator<CourseWriteContext> validator, TimeProvider timeProvider) : IDeleteCourse
{
    public async Task ExecuteAsync(DeleteCourseInput request, CancellationToken cancellationToken)
    {
        var input = request.Context;
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        await using var transaction = await edits.LockAsync(input.CourseId, cancellationToken);
        var key = "delete:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{input.CourseId:D}:{input.IdempotencyKey}")));
        var receipt = await edits.FindAsync(new(input.TenantId, input.ActorId, key), cancellationToken);
        var now = timeProvider.GetUtcNow();
        // The receipt has no course foreign key and must be read before the deleted aggregate.
        if (receipt is not null && receipt.ExpiresAt > now) return;
        var course = await courses.GetAsync(input.CourseId, cancellationToken);
        NotFoundException.ThrowIfNull(course, "Course not found.");
        course!.EnsureNeverPublished();
        if (await versions.HasPublishedAsync(course.Id, cancellationToken))
            throw new CourseRuleException("COURSE_ALREADY_PUBLISHED");
        courses.Remove(course);
        if (receipt is null) { receipt = CourseEditReceipt.Create(input.TenantId, input.ActorId, key); edits.Add(receipt); }
        receipt.Store(string.Empty, "{\"status\":204}", now);
        await unitOfWork.CommitAsync(cancellationToken);
        await transaction.CompleteAsync(CancellationToken.None);
    }
}
