using System;
using System.IO;

namespace EegScreenCapture.Utils
{
    public static class Logger
    {
        private static readonly string LogFilePath = "eeg-capture-debug.log";
        private static readonly object LockObject = new object();

        public static void Log(string message)
        {
            lock (LockObject)
            {
                try
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var logMessage = $"[{timestamp}] {message}";
                    File.AppendAllText(LogFilePath, logMessage + Environment.NewLine);
                    Console.WriteLine(logMessage); // Also to console in case it's visible
                }
                catch
                {
                    // Ignore logging errors
                }
            }
        }

        public static void LogError(string message, Exception ex)
        {
            Log($"ERROR: {message}");
            Log($"Exception: {ex.Message}");
            Log($"Stack Trace: {ex.StackTrace}");
        }
    }
}
