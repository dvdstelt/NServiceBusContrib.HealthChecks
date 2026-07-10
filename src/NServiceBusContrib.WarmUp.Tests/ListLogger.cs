using Microsoft.Extensions.Logging;

namespace NServiceBusContrib.WarmUp.Tests;

/// <summary>
/// An <see cref="ILogger{T}"/> that records the level and formatted message of each entry.
/// Thread-safe: the background monitor logs from a worker thread while tests poll for entries.
/// </summary>
sealed class ListLogger<T> : ILogger<T>
{
    readonly Lock gate = new();
    readonly List<(LogLevel Level, string Message)> entries = [];

    public IReadOnlyList<(LogLevel Level, string Message)> Entries
    {
        get
        {
            lock (gate)
            {
                return entries.ToArray();
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (gate)
        {
            entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
