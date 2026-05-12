using System;
using System.Diagnostics;
using System.IO;

namespace FopFinance
{
    internal static class AppLogger
    {
        private static readonly object _sync = new();

        private static string LogFilePath =>
            Path.Combine(AppPaths.LogDir, $"app_{DateTime.Now:yyyyMMdd}.log");

        public static void Info(string message) => Write("INFO", message);

        public static void Error(string message, Exception? ex = null)
        {
            string detail = ex == null
                ? message
                : $"{message} | {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}";
            Write("ERROR", detail);
        }

        private static void Write(string level, string message)
        {
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
            lock (_sync)
            {
                try { File.AppendAllText(LogFilePath, line + Environment.NewLine); }
                catch { /* Never crash the app due to a logging failure. */ }
            }
            Debug.WriteLine(line);
        }
    }
}
