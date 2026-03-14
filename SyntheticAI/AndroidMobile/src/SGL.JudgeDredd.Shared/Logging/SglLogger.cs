using Serilog;
using Serilog.Events;

namespace SGL.JudgeDredd.Shared.Logging;

public static class SglLogger
{
    private static bool _initialized;

    public static void Initialize(string logDirectory)
    {
        if (_initialized)
            return;

        Directory.CreateDirectory(logDirectory);

        var logFilePath = Path.Combine(logDirectory, "sgl-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: 50 * 1024 * 1024,
                retainedFileCountLimit: 31,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        _initialized = true;
    }

    public static void Information(string message, params object[] args)
    {
        Log.Information(message, args);
    }

    public static void Warning(string message, params object[] args)
    {
        Log.Warning(message, args);
    }

    public static void Error(string message, Exception? ex = null, params object[] args)
    {
        if (ex != null)
            Log.Error(ex, message, args);
        else
            Log.Error(message, args);
    }

    public static void Debug(string message, params object[] args)
    {
        Log.Debug(message, args);
    }

    public static void CloseAndFlush()
    {
        Log.CloseAndFlush();
    }
}
