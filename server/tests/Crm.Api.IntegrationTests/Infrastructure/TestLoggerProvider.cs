using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Crm.Api.IntegrationTests.Infrastructure;

public sealed record LogEntry(
    string Category, LogLevel Level, string Message, Exception? Exception, IReadOnlyList<object?> Scopes);

/// <summary>In-memory logger that records every entry together with its active scopes.</summary>
public sealed class TestLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    public IReadOnlyCollection<LogEntry> Entries => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new TestLogger(categoryName, this);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public void Dispose()
    {
    }

    private sealed class TestLogger(string category, TestLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            provider._scopes.Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var scopes = new List<object?>();
            provider._scopes.ForEachScope((scope, list) => list.Add(scope), scopes);
            provider._entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception), exception, scopes));
        }
    }
}
