using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using FopFinance.Managers;

namespace FopFinance
{
    /// <summary>
    /// Головне вікно програми.
    /// Містить WebView2-контрол, який завантажує локальний HTML-інтерфейс.
    /// C# ↔ JS зв'язок реалізований через HostObjects (синхронний міст).
    /// </summary>
    public partial class MainForm : Form
    {
        private WebView2? _webView;
        private FinanceManager _manager;
        private BridgeService  _bridge;
        private readonly Timer _autoSaveTimer;

        public MainForm()
        {
            InitializeComponent();
            AppLogger.Info("MainForm ctor started");

            // Налаштовуємо форму
            Text        = "ФОП Фінанси";
            Width        = 1200;
            Height       = 760;
            MinimumSize  = new System.Drawing.Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;

            // Менеджер і міст
            _manager = new FinanceManager();
            _bridge  = new BridgeService(_manager, this);

            _autoSaveTimer = new Timer { Interval = 60_000 };
            _autoSaveTimer.Tick += (_, _) => _bridge.SaveAutoBackup();
            _autoSaveTimer.Start();

            FormClosing += (_, _) => _bridge.SaveAutoBackup();

            // Ініціалізація WebView2
            InitWebView();
        }

        private async void InitWebView()
        {
            try
            {
                _webView = new WebView2 { Dock = DockStyle.Fill };
                Controls.Add(_webView);

                // Папка для даних WebView2 (кеш, cookies тощо)
                string dataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FopFinance", "WebView2");

                AppLogger.Info($"InitWebView: creating environment. DataDir={dataDir}");
                var env = await CoreWebView2Environment.CreateAsync(null, dataDir);
                await _webView.EnsureCoreWebView2Async(env);
                AppLogger.Info("InitWebView: CoreWebView2 initialized");

                _webView.CoreWebView2.NavigationStarting += (_, e) =>
                    AppLogger.Info($"NavigationStarting: {e.Uri}");
                _webView.CoreWebView2.NavigationCompleted += (_, e) =>
                    AppLogger.Info($"NavigationCompleted: success={e.IsSuccess}, status={e.WebErrorStatus}");
                _webView.CoreWebView2.ProcessFailed += (_, e) =>
                    AppLogger.Error($"WebView process failed: {e.ProcessFailedKind}");

                // Реєструємо C#-об'єкт у JS як hostObjects.bridge.
                _webView.CoreWebView2.AddHostObjectToScript("bridge", _bridge);
                AppLogger.Info("InitWebView: host object 'bridge' added");

                // Додаємо сумісність із існуючим JS-кодом, який очікує window.bridge.
                await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                    "window.bridge = window.chrome?.webview?.hostObjects?.sync?.bridge ?? null;");
                AppLogger.Info("InitWebView: bridge alias script registered");

                // Відключаємо кнопки назад/вперед та контекстне меню (не потрібні для десктоп-застосунку)
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

                // Завантажуємо локальний index.html
                string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                               "wwwroot", "index.html");
                if (!File.Exists(htmlPath))
                {
                    AppLogger.Error($"InitWebView: index.html not found at {htmlPath}");
                    MessageBox.Show(this,
                        $"Не знайдено UI файл:\n{htmlPath}",
                        "Помилка запуску",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                string url = "file:///" + htmlPath.Replace("\\", "/");
                AppLogger.Info($"InitWebView: navigating to {url}");
                _webView.CoreWebView2.Navigate(url);
            }
            catch (Exception ex)
            {
                AppLogger.Error("InitWebView failed", ex);
                MessageBox.Show(this,
                    "Помилка ініціалізації WebView2. Деталі в логах.",
                    "WebView2",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // Стандартний InitializeComponent (мінімальний варіант без дизайнера)
        private void InitializeComponent()
        {
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize    = new System.Drawing.Size(1200, 760);
            Name          = "MainForm";
            ResumeLayout(false);
        }
    }
}
