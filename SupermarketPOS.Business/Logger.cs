using System;
using System.Diagnostics;
using System.IO;

namespace SupermarketPOS.Business
{
    public static class Logger
    {
        private static readonly string _logPath;
        private static bool _logPathAvailable;

        static Logger()
        {
            try
            {
                _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                if (!Directory.Exists(_logPath))
                    Directory.CreateDirectory(_logPath);

                _logPathAvailable = true;
            }
            catch (Exception ex)
            {
                _logPathAvailable = false;
                FallbackLog("ERROR", $"Failed to initialize log directory: {ex.Message}");
            }
        }

        public static void Debug(string message) => Log("DEBUG", message);
        public static void Info(string message) => Log("INFO", message);
        public static void Warning(string message) => Log("WARN", message);
        public static void Warning(Exception ex, string message) => Log("WARN", $"{message}: {ex.Message}");
        public static void Error(string message) => Log("ERROR", message);
        public static void Error(Exception ex, string message) => Log("ERROR", $"{message}{Environment.NewLine}{ex}");
        public static void Fatal(Exception ex, string message) => Log("FATAL", $"{message}{Environment.NewLine}{ex}");

        private static void Log(string level, string message)
        {
            string formattedMessage = $"{DateTime.Now:HH:mm:ss} [{level}] {message}";

            try
            {
                if (_logPathAvailable)
                {
                    var logFile = Path.Combine(_logPath, $"{DateTime.Now:yyyyMMdd}.log");
                    File.AppendAllText(logFile, formattedMessage + Environment.NewLine);
                }
                else
                {
                    // Primary log path unavailable, use fallback
                    FallbackLog(level, message);
                }
            }
            catch (Exception ex)
            {
                // Primary logging failed, attempt fallback
                FallbackLog("ERROR", $"Logging failed: {ex.Message}. Original [{level}]: {message}");
            }
        }

        /// <summary>
        /// Fallback logging to Debug output and Windows Event Log when file logging fails.
        /// EventLog is only used if the source was pre-created during installation.
        /// </summary>
        private static void FallbackLog(string level, string message)
        {
            string formattedMessage = $"{DateTime.Now:HH:mm:ss} [{level}] {message}";

            // 1. Always write to Debug output (visible in Visual Studio / DebugView)
            System.Diagnostics.Debug.WriteLine($"[SupermarketPOS] {formattedMessage}");

            // 2. Write to Windows Event Log for critical errors (only if source already exists)
            if (level == "ERROR" || level == "FATAL")
            {
                try
                {
                    if (EventLog.SourceExists("SupermarketPOS"))
                    {
                        EventLog.WriteEntry("SupermarketPOS", formattedMessage, EventLogEntryType.Error);
                    }
                }
                catch
                {
                    // Event Log unavailable — final fallback: Console
                    Console.Error.WriteLine($"[EVENTLOG_FAILED] {formattedMessage}");
                }
            }
        }
    }
}