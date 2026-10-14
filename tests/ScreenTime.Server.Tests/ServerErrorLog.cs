using Microsoft.Extensions.Logging;

namespace ScreenTime.Server.Tests;

/// <summary>
/// Appends server errors (with their exceptions) from test runs to a file, so a rare failure that only
/// happens under load can be diagnosed without slowing every run down with detailed console logging.
/// </summary>
public sealed class ServerErrorLog : ILoggerProvider
{
    public static readonly string FilePath = Path.Combine(Path.GetTempPath(), "screentime-test-server-errors.log");
    private static readonly Lock WriteLock = new();

    // Start each test run with an empty log.
    static ServerErrorLog()
    {
        try
        {
            File.Delete(FilePath);
        }
        catch (IOException)
        {
            // Still open elsewhere; appending to the old log is fine.
        }
    }

    public ILogger CreateLogger(string categoryName) => new ErrorLogger(categoryName);

    public void Dispose() { }

    private sealed class ErrorLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;
            lock (WriteLock)
                File.AppendAllText(FilePath, $"[{DateTime.Now:O}] {logLevel} {category}: {formatter(state, exception)}\n{exception}\n\n");
        }
    }
}
