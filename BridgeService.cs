using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using FopFinance.Managers;
using FopFinance.Models;
using FopFinance.Storage;

namespace FopFinance
{
    /// <summary>
    /// Міст між C# та JavaScript (WebView2).
    /// Методи цього класу реєструються як JS-об'єкт window.bridge
    /// і викликаються з браузерного коду через window.chrome.webview.hostObjects.bridge.*
    ///
    /// Повертає дані завжди у форматі JSON-рядка, щоб JS міг їх розібрати.
    /// </summary>
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class BridgeService
    {
        private readonly FinanceManager _manager;
        private readonly Form _mainForm; // потрібен для діалогів збереження/відкриття

        // Параметри серіалізації (camelCase для зручності у JS)
        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
            WriteIndented          = false
        };

        public BridgeService(FinanceManager manager, Form mainForm)
        {
            _manager  = manager;
            _mainForm = mainForm;
            AppLogger.Info("BridgeService created");
        }

        /// <summary>Службовий метод для перевірки з'єднання між JS і C#.</summary>
        public string Ping() => Ok("pong");

        /// <summary>Отримує лог-повідомлення з frontend.</summary>
        public string LogClient(string message)
        {
            AppLogger.Info($"Frontend: {message}");
            return Ok();
        }

        // ===================== ПРОФІЛЬ ФОП =====================

        /// <summary>Повертає JSON-рядок з даними підприємця.</summary>
        public string GetEntrepreneur() =>
            JsonSerializer.Serialize(_manager.Entrepreneur, _jsonOpts);

        /// <summary>Оновлює дані підприємця. json — серіалізований Entrepreneur.</summary>
        public string SaveEntrepreneur(string json)
        {
            try
            {
                var e = JsonSerializer.Deserialize<Entrepreneur>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (e != null) _manager.Entrepreneur = e;
                return Ok();
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        // ===================== ДОХОДИ =====================

        public string GetIncomes() =>
            JsonSerializer.Serialize(_manager.Incomes, _jsonOpts);

        public string AddIncome(string json)
        {
            try
            {
                var income = Deserialize<Income>(json);
                if (income == null) return Error("Некоректні дані.");
                string err = _manager.AddIncome(income);
                return string.IsNullOrEmpty(err) ? Ok(income.Id) : Error(err);
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        public string UpdateIncome(string json)
        {
            try
            {
                var income = Deserialize<Income>(json);
                if (income == null) return Error("Некоректні дані.");
                string err = _manager.UpdateIncome(income);
                return string.IsNullOrEmpty(err) ? Ok() : Error(err);
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        // ===================== ВИТРАТИ =====================

        public string GetExpenses() =>
            JsonSerializer.Serialize(_manager.Expenses, _jsonOpts);

        public string AddExpense(string json)
        {
            try
            {
                var expense = Deserialize<Expense>(json);
                if (expense == null) return Error("Некоректні дані.");
                string err = _manager.AddExpense(expense);
                return string.IsNullOrEmpty(err) ? Ok(expense.Id) : Error(err);
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        public string UpdateExpense(string json)
        {
            try
            {
                var expense = Deserialize<Expense>(json);
                if (expense == null) return Error("Некоректні дані.");
                string err = _manager.UpdateExpense(expense);
                return string.IsNullOrEmpty(err) ? Ok() : Error(err);
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        // ===================== ВИДАЛЕННЯ =====================

        public string RemoveRecord(string id)
        {
            bool ok = _manager.RemoveRecord(id);
            return ok ? Ok() : Error("Запис не знайдено.");
        }

        // ===================== КАТЕГОРІЇ =====================

        public string GetCategories() =>
            JsonSerializer.Serialize(_manager.Categories, _jsonOpts);

        public string AddCategory(string json)
        {
            try
            {
                var cat = Deserialize<Category>(json);
                if (cat == null) return Error("Некоректні дані.");
                string err = _manager.AddCategory(cat);
                return string.IsNullOrEmpty(err) ? Ok(cat.Id) : Error(err);
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        public string UpdateCategory(string json)
        {
            try
            {
                var cat = Deserialize<Category>(json);
                if (cat == null) return Error("Некоректні дані.");
                string err = _manager.UpdateCategory(cat);
                return string.IsNullOrEmpty(err) ? Ok() : Error(err);
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        public string RemoveCategory(string id)
        {
            string err = _manager.RemoveCategory(id);
            return string.IsNullOrEmpty(err) ? Ok() : Error(err);
        }

        // ===================== ЗВІТ =====================

        /// <summary>Генерує звіт за період. Параметри: "startDate|endDate" (ISO 8601).</summary>
        public string GenerateReport(string startIso, string endIso)
        {
            try
            {
                if (!DateTime.TryParse(startIso, out DateTime start))
                    return Error("Невірна дата початку.");
                if (!DateTime.TryParse(endIso, out DateTime end))
                    return Error("Невірна дата кінця.");

                var report = _manager.GenerateReport(start, end);
                return JsonSerializer.Serialize(report, _jsonOpts);
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        /// <summary>Експортує звіт у файл (діалог збереження). format = "json" | "xml".</summary>
        public string ExportReport(string reportJson, string format)
        {
            try
            {
                var report = JsonSerializer.Deserialize<Report>(reportJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (report == null) return Error("Некоректний звіт.");

                string content = format == "xml" ? report.ExportToXML() : report.ExportToJSON();
                string ext = format == "xml" ? "xml" : "json";

                // ShowSaveDialog повинен виконуватись у UI-потоці
                string? filePath = null;
                _mainForm.Invoke(() =>
                {
                    using var dlg = new SaveFileDialog
                    {
                        Filter   = $"{ext.ToUpper()} файл|*.{ext}",
                        FileName = $"report_{DateTime.Today:yyyyMMdd}.{ext}"
                    };
                    if (dlg.ShowDialog(_mainForm) == DialogResult.OK)
                        filePath = dlg.FileName;
                });

                if (filePath == null) return Ok("cancelled");
                File.WriteAllText(filePath, content);
                return Ok(filePath);
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        // ===================== ЗБЕРЕЖЕННЯ / ЗАВАНТАЖЕННЯ =====================

        /// <summary>Зберігає базу у файл (діалог вибору формату і шляху).</summary>
        public string SaveData(string format)
        {
            string? filePath = null;
            _mainForm.Invoke(() =>
            {
                string ext = format == "xml" ? "xml" : "json";
                using var dlg = new SaveFileDialog
                {
                    Filter   = $"{ext.ToUpper()} файл|*.{ext}",
                    FileName = $"fop_data.{ext}"
                };
                if (dlg.ShowDialog(_mainForm) == DialogResult.OK)
                    filePath = dlg.FileName;
            });

            if (filePath == null) return Ok("cancelled");

            try
            {
                if (format == "xml")
                    DataStorage.SaveToXML(filePath,
                        _manager.Entrepreneur,
                        _manager.Incomes,
                        _manager.Expenses,
                        _manager.Categories);
                else
                    DataStorage.SaveToJSON(filePath,
                        _manager.Entrepreneur,
                        _manager.Incomes,
                        _manager.Expenses,
                        _manager.Categories);

                return Ok(filePath);
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        /// <summary>Завантажує базу з файлу (діалог вибору файлу).</summary>
        public string LoadData()
        {
            string? filePath = null;
            _mainForm.Invoke(() =>
            {
                using var dlg = new OpenFileDialog
                {
                    Filter = "Файли даних (*.json;*.xml)|*.json;*.xml|JSON|*.json|XML|*.xml"
                };
                if (dlg.ShowDialog(_mainForm) == DialogResult.OK)
                    filePath = dlg.FileName;
            });

            if (filePath == null) return Ok("cancelled");

            try
            {
                bool isXml = filePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

                var (entrepreneur, incomes, expenses, categories) = isXml
                    ? DataStorage.LoadFromXML(filePath)
                    : DataStorage.LoadFromJSON(filePath);

                _manager.LoadData(incomes, expenses, categories, entrepreneur);
                return Ok("loaded");
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        // ===================== Утиліти =====================

        private static T? Deserialize<T>(string json) =>
            JsonSerializer.Deserialize<T>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        /// <summary>Формує JSON-відповідь успіху { ok: true, data: "..." }.</summary>
        private static string Ok(string data = "") =>
            $"{{\"ok\":true,\"data\":\"{data.Replace("\"", "\\\"")}\"}}";

        /// <summary>Формує JSON-відповідь помилки { ok: false, error: "..." }.</summary>
        private static string Error(string msg) =>
            $"{{\"ok\":false,\"error\":\"{msg.Replace("\"", "\\\"")}\"}}";
    }
}
