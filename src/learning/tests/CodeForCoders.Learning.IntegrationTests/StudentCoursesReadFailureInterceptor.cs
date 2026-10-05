using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CodeForCoders.Learning.IntegrationTests;

public sealed class StudentCoursesReadFailureInterceptor : DbCommandInterceptor
{
    public Exception? Failure { get; set; }
    public string Table { get; set; } = "progress.lesson_progress";

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (Failure is not null && command.CommandText.Contains(Table, StringComparison.Ordinal)) throw Failure;
        return ValueTask.FromResult(result);
    }
}
