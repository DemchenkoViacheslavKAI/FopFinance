using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace FopFinance
{
    /// <summary>
    /// Main application window.
    /// Hosts a WebView2 control that loads the local HTML UI.
    /// C# ↔ JS communication is handled via HostObjects (synchronous bridge).
    /// </summary>
    public partial class MainForm : Form
    {
        private WebView2?      _webView;
        private BridgeService  _bridge;
        private readonly Timer _autoSaveTimer;

        public MainForm()
        {
            InitializeComponent();
            AppLogger.Info("MainForm ctor started");

            Text          = "ФОП Фінанси";
            Width         = 1200;
            Height        = 760;
            MinimumSize   = new System.Drawing.Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;

            // BridgeService now owns its own dependency graph
            _bridge = new BridgeService(this);

            _autoSaveTimer = new Timer { Interval = 60_000 };
            _autoSaveTimer.Tick    += (_, _) => _bridge.SaveAutoBackup();
            _autoSaveTimer.Start();

            FormClosing += (_, _) => _bridge.SaveAutoBackup();

            InitWebView();
        }

        private async void InitWebView()
        {
            try
            {
                _webView = new WebView2 { Dock = DockStyle.Fill };
                Controls.Add(_webView);

                AppLogger.Info($"InitWebView: creating environment. DataDir={AppPaths.WebViewDir}");
                var env = await CoreWebView2Environment.CreateAsync(null, AppPaths.WebViewDir);
                await _webView.EnsureCoreWebView2Async(env);
                AppLogger.Info("InitWebView: CoreWebView2 initialized");

                _webView.CoreWebView2.NavigationStarting  += (_, e) => AppLogger.Info($"NavigationStarting: {e.Uri}");
                _webView.CoreWebView2.NavigationCompleted += (_, e) => AppLogger.Info($"NavigationCompleted: success={e.IsSuccess}");
                _webView.CoreWebView2.ProcessFailed       += (_, e) => AppLogger.Error($"WebView process failed: {e.ProcessFailedKind}");

                _webView.CoreWebView2.AddHostObjectToScript("bridge", _bridge);
                AppLogger.Info("InitWebView: host object 'bridge' added");

                await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                    "window.bridge = window.chrome?.webview?.hostObjects?.sync?.bridge ?? null;");

                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled            = false;

                string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "index.html");
                if (!File.Exists(htmlPath))
                {
                    AppLogger.Error($"InitWebView: index.html not found at {htmlPath}");
                    MessageBox.Show(this, $"Не знайдено UI файл:\n{htmlPath}", "Помилка запуску",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string url = "file:///" + htmlPath.Replace("\\", "/");
                AppLogger.Info($"InitWebView: navigating to {url}");
                _webView.CoreWebView2.Navigate(url);
            }
            catch (Exception ex)
            {
                AppLogger.Error("InitWebView failed", ex);
                MessageBox.Show(this, "Помилка ініціалізації WebView2. Деталі в логах.",
                    "WebView2", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

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
