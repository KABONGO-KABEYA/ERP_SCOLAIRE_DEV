using Microsoft.Extensions.Logging;

namespace SchoolManagement.UpdateAgent;

internal sealed class AgentFileLoggerProvider : ILoggerProvider
{
    private readonly string _directory;
    private readonly object _gate = new();

    public AgentFileLoggerProvider(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public ILogger CreateLogger(string categoryName) => new AgentFileLogger(_directory, _gate, categoryName);

    public void Dispose()
    {
    }
}

internal sealed class AgentFileLogger : ILogger
{
    private readonly string _directory;
    private readonly object _gate;
    private readonly string _category;

    public AgentFileLogger(string directory, object gate, string category)
    {
        _directory = directory;
        _gate = gate;
        _category = category;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var line = $"{DateTime.UtcNow:O} {logLevel} {_category} {formatter(state, exception)}";
        if (exception is not null)
        {
            line += " " + exception.GetType().Name + ": " + exception.Message;
        }

        var path = Path.Combine(_directory, $"update-agent-{DateTime.UtcNow:yyyyMMdd}.log");
        lock (_gate)
        {
            File.AppendAllText(path, line + Environment.NewLine);
        }
    }

    private sealed class NullScope : IDisposable
    {
        internal static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
