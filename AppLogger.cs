using System;
using System.Diagnostics;
using System.IO;

namespace FopFinance
{
    internal static class AppLogger
    {
        private static readonly object _sync = new();

        public static string LogFilePath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FopFinance",
                    "logs");

                Directory.CreateDirectory(dir);
                return Path.Combine(dir, $"app_{DateTime.Now:yyyyMMdd}.log");
            }
        }

        public static void Info(string message) => Write("INFO", message);

        public static void Error(string message, Exception? ex = null)
        {
            if (ex == null)
            {
                Write("ERROR", message);
                return;
            }

            Write("ERROR", $"{message} | {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
        }

        private static void Write(string level, string message)
        {
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";

            lock (_sync)
            {
                try
                {
                    File.AppendAllText(LogFilePath, line + Environment.NewLine);
                }
                catch
                {
                    // Avoid crashing app because of logging issues.
                }
            }

            Debug.WriteLine(line);
        }
    }
}