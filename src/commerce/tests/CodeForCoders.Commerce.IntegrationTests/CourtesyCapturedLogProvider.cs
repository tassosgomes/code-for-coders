using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace CodeForCoders.Commerce.IntegrationTests;

public sealed class CourtesyCapturedLogProvider(ConcurrentQueue<string> logs) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new LookupLogger(logs);
    public void Dispose() { }
    private sealed class LookupLogger(ConcurrentQueue<string> logs) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => logs.Enqueue(formatter(state, exception) + " " + exception);
    }
}
