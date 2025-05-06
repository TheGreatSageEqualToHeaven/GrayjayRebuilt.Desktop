// DotCef, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// DotCef.Logger

namespace DotCef;

public static class Logger
{
    public static Action<LogLevel, string, string, Exception?> LogCallback =
        delegate(LogLevel level, string tag, string message, Exception? ex)
        {
            var value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var value2 = level.ToString().ToUpper();
            var text = $"[{value}] [{value2}] [{tag}] {message}";
            if (ex != null) text = text + "\nException: " + ex.Message + "\nStack Trace: " + ex.StackTrace;
            Console.WriteLine(text);
        };

    public static Func<LogLevel, bool> WillLog = level => true;

    internal static void Debug<T>(string message, Exception? ex = null)
    {
        LogCallback(LogLevel.Debug, "T", message, ex);
    }

    internal static void Verbose<T>(string message, Exception? ex = null)
    {
        LogCallback(LogLevel.Verbose, "T", message, ex);
    }

    internal static void Info<T>(string message, Exception? ex = null)
    {
        LogCallback(LogLevel.Info, "T", message, ex);
    }

    internal static void Warning<T>(string message, Exception? ex = null)
    {
        LogCallback(LogLevel.Warning, "T", message, ex);
    }

    internal static void Error<T>(string message, Exception? ex = null)
    {
        LogCallback(LogLevel.Error, "T", message, ex);
    }

    internal static void Debug(string tag, string message, Exception? ex = null)
    {
        LogCallback(LogLevel.Debug, tag, message, ex);
    }

    internal static void Verbose(string tag, string message, Exception? ex = null)
    {
        LogCallback(LogLevel.Verbose, tag, message, ex);
    }

    internal static void Info(string tag, string message, Exception? ex = null)
    {
        LogCallback(LogLevel.Info, tag, message, ex);
    }

    internal static void Warning(string tag, string message, Exception? ex = null)
    {
        LogCallback(LogLevel.Warning, tag, message, ex);
    }

    internal static void Error(string tag, string message, Exception? ex = null)
    {
        LogCallback(LogLevel.Error, tag, message, ex);
    }
}