using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Learning.Application.Exceptions;
using CodeForCoders.Learning.Application.Interfaces;
using CodeForCoders.Learning.Application.UseCases.Courses.Common;
using CodeForCoders.Learning.Domain.Entities;
using CodeForCoders.Learning.Domain.Repositories;
using CodeForCoders.Learning.Domain.SeedWork;
using FluentValidation;

namespace CodeForCoders.Learning.Application.UseCases.Courses.PublishCourse;

public sealed class PublishCourse(ICourseRepository courses, ICourseEditStore receipts, ICourseVersionStore versions,
    IOutboxMessageWriter outbox, IUnitOfWork unitOfWork, IValidator<CourseWriteContext> validator, TimeProvider clock) : IPublishCourse
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CourseVersionOutput> ExecuteAsync(PublishCourseInput input, CancellationToken cancellationToken)
    {
        var context = input.Context;
        await validator.ValidateAndThrowAsync(context, cancellationToken);
        if (input.DraftRevision < 1) throw new CourseRuleException("INVALID_REQUEST");
        await using var transaction = await receipts.LockAsync(context.CourseId, cancellationToken);
        var course = await courses.GetAsync(context.CourseId, cancellationToken);
        NotFoundException.ThrowIfNull(course, "Course not found.");
        var key = "publish:" + Hash($"{context.CourseId:D}:{context.IdempotencyKey}");
        var receipt = await receipts.FindAsync(new(context.TenantId, context.ActorId, key), cancellationToken);
        var hash = Hash(JsonSerializer.Serialize(new { input.DraftRevision, input.VersionNote }, JsonOptions));
        var now = clock.GetUtcNow();
        if (receipt is not null && receipt.ExpiresAt > now)
        {
            if (receipt.RequestHash != hash) throw new CourseRuleException("IDEMPOTENCY_KEY_REUSED");
            return JsonSerializer.Deserialize<CourseVersionOutput>(receipt.ResponseJson, JsonOptions)!;
        }
        var recommended = await versions.GetCurrentReferencesAsync(course!.RecommendedCourseIds, cancellationToken);
        var version = course.Publish(new(input.DraftRevision, input.VersionNote,
            new(context.TenantId, context.ActorId, context.ActorName, course.Title, course.Description, now), recommended));
        versions.Add(version);
        await AppendMessagesAsync(version, cancellationToken);
        var output = CourseVersionOutput.FromVersion(version);
        if (receipt is null) { receipt = CourseEditReceipt.Create(context.TenantId, context.ActorId, key); receipts.Add(receipt); }
        receipt.Store(hash, JsonSerializer.Serialize(output, JsonOptions), now);
        await unitOfWork.CommitAsync(cancellationToken);
        await transaction.CompleteAsync(CancellationToken.None);
        return output;
    }

    private async Task AppendMessagesAsync(CourseVersion version, CancellationToken cancellationToken)
    {
        var fact = new
        {
            EventId = version.Id,
            version.TenantId,
            version.CourseId,
            version.VersionNumber,
            version.PublishedAt,
            version.PublishedById,
            version.Title,
            Description = version.Description ?? string.Empty,
            version.Level,
            Prerequisite = version.Prerequisite ?? new PublishedPrerequisite(null, []),
            Modules = version.Modules.Select(module => new
            {
                module.ModuleId,
                module.Title,
                module.Position,
                Lessons = module.Lessons.Select(lesson => new { lesson.LessonId, lesson.Title, lesson.Position, lesson.VideoId })
            }),
        };
        const string factRoute = "conteudo.versao-publicada.v1";
        await outbox.AppendAsync(new(version.Id, version.TenantId, factRoute, factRoute, fact, version.PublishedAt, Activity.Current?.Id), cancellationToken);
        var act = new
        {
            FatoId = version.Id,
            Origem = "conteudo",
            Tipo = "versao-publicada",
            version.TenantId,
            PraticadoEm = version.PublishedAt,
            Autor = new { Tipo = "conta-interna", Id = version.PublishedById },
            Alvo = new { Tipo = "curso", Id = version.CourseId },
            Complemento = new Dictionary<string, string> { ["versao"] = version.VersionNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) },
        };
        const string actRoute = "auditoria.ato-praticado.v1";
        await outbox.AppendAsync(new(Guid.CreateVersion7(), version.TenantId, actRoute, actRoute, act, version.PublishedAt, Activity.Current?.Id), cancellationToken);
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
