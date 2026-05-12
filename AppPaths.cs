using System;
using System.IO;

namespace FopFinance
{
    /// <summary>
    /// Centralises all application file-system paths (KISS / DRY).
    /// </summary>
    internal static class AppPaths
    {
        private static readonly string BaseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FopFinance");

        public static string DataDir    => EnsureDir(Path.Combine(BaseDir, "data"));
        public static string LogDir     => EnsureDir(Path.Combine(BaseDir, "logs"));
        public static string WebViewDir => EnsureDir(Path.Combine(BaseDir, "WebView2"));

        public static string DatabasePath => Path.Combine(DataDir, "fopfinance.db");

        private static string EnsureDir(string path)
        {
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
