using System;
using System.Windows.Forms;

namespace FopFinance
{
    /// <summary>
    /// Точка входу у застосунок.
    /// Налаштовує рендеринг та запускає головну форму.
    /// </summary>
    internal static class Program
    {
        [STAThread] // обов'язково для WinForms + WebView2
        static void Main()
        {
            Application.ThreadException += (_, e) =>
                AppLogger.Error("Unhandled UI thread exception", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                AppLogger.Error("Unhandled AppDomain exception", e.ExceptionObject as Exception);

            AppLogger.Info("Application startup");

            // Вмикаємо DPI-масштабування для чіткого відображення на HiDPI-моніторах
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.Run(new MainForm());
        }
    }
}
