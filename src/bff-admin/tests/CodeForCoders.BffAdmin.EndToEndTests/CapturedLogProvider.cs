using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;

namespace CodeForCoders.BffAdmin.EndToEndTests;

public sealed class CapturedLogProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<string> entries = new();
    private IExternalScopeProvider scopeProvider = new LoggerExternalScopeProvider();

    public IReadOnlyList<string> Entries => entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new CapturedLogger(this, categoryName);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => this.scopeProvider = scopeProvider;

    public void Dispose()
    {
    }

    private void Add(string entry) => entries.Enqueue(entry);

    private sealed class CapturedLogger(CapturedLogProvider provider, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => provider.scopeProvider.Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var entry = new StringBuilder()
                .Append(logLevel).Append(' ').Append(categoryName).Append(' ')
                .Append(formatter(state, exception));
            AppendValues(entry, state);
            provider.scopeProvider.ForEachScope((scope, builder) => AppendValues(builder, scope), entry);
            if (exception is not null)
            {
                entry.Append(' ').Append(exception);
            }

            provider.Add(entry.ToString());
        }

        private static void AppendValues(StringBuilder entry, object? state)
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> values)
            {
                foreach (var value in values)
                {
                    entry.Append(' ').Append(value.Key).Append('=').Append(value.Value);
                }
            }
            else if (state is not null)
            {
                entry.Append(' ').Append(state);
            }
        }
    }
}
