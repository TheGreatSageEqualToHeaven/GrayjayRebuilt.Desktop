// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// SyncShared.Logger

namespace SyncShared;

public static class Logger
{
    public const LogLevel DefaultLogLevel = LogLevel.Verbose;

    public static Action<LogLevel, string, string, Exception?> LogCallback =
        delegate(LogLevel level, string tag, string message, Exception? ex)
        {
            if (WillLog(level))
            {
                var value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var value2 = level.ToString().ToUpper();
                var text = $"[{value}] [{value2}] [{tag}] {message}";
                if (ex != null) text = text + "\nException: " + ex.Message + "\nStack Trace: " + ex.StackTrace;
                Console.WriteLine(text);
            }
        };

    public static Func<LogLevel, bool> WillLog = level => level <= LogLevel.Verbose;

    public static void Debug<T>(string message, Exception? ex = null)
    {
        LogCallback(LogLevel.Debug, typeof(T).Name, message, ex);
    }

    public static void Verbose<T>(string message, Exception? ex = null)
    {
        LogCallback(LogLevel.Verbose, typeof(T).Name, message, ex);
    }

    public static void Info<T>(string message, Exception? ex = null)
    {
        LogCallback(LogLevel.Info, typeof(T).Name, message, ex);
    }

    public static void Warning<T>(string message, Exception? ex = null)
    {
        LogCallback(LogLevel.Warning, typeof(T).Name, message, ex);
    }

    public static void Error<T>(string message, Exception? ex = null)
    {
        LogCallback(LogLevel.Error, typeof(T).Name, message, ex);
    }

    public static void Debug(string tag, string message, Exception? ex = null)
    {
        LogCallback(LogLevel.Debug, tag, message, ex);
    }

    public static void Verbose(string tag, string message, Exception? ex = null)
    {
        LogCallback(LogLevel.Verbose, tag, message, ex);
    }

    public static void Info(string tag, string message, Exception? ex = null)
    {
        LogCallback(LogLevel.Info, tag, message, ex);
    }

    public static void Warning(string tag, string message, Exception? ex = null)
    {
        LogCallback(LogLevel.Warning, tag, message, ex);
    }

    public static void Error(string tag, string message, Exception? ex = null)
    {
        LogCallback(LogLevel.Error, tag, message, ex);
    }
}