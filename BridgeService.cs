using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using ClosedXML.Excel;
using FopFinance.Contracts;
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
        private const int MaxAutoBackups = 10;
        private readonly string _sqlitePath;
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

            var dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FopFinance",
                "data");
            Directory.CreateDirectory(dataDir);
            _sqlitePath = Path.Combine(dataDir, "fopfinance.db");

            SqliteStorage.EnsureDatabase(_sqlitePath);
            var profiles = SqliteStorage.GetProfiles(_sqlitePath);
            if (profiles.Count == 0)
            {
                var created = SqliteStorage.AddProfile(_sqlitePath, "Основний профіль");
                profiles.Add(created);
            }

            _manager.SetProfiles(profiles, profiles[0].Id);
            LoadActiveProfileFromPrimaryStorage();
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

                if (e == null)
                    return Error("Некоректні дані профілю.");

                if (string.IsNullOrWhiteSpace(e.FullName))
                    return Error("ПІБ є обов'язковим.");

                if (e.TaxGroup < 1 || e.TaxGroup > 3)
                    return Error("Група ЄП повинна бути в діапазоні 1..3.");

                _manager.Entrepreneur = e;
                PersistActiveProfileToPrimaryStorage();
                return Ok();
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        public string GetProfiles() =>
            JsonSerializer.Serialize(_manager.Profiles, _jsonOpts);

        public string AddProfile(string profileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(profileName))
                    return Error("Назва профілю не може бути порожньою.");

                bool exists = _manager.Profiles.Exists(p =>
                    p.Name.Equals(profileName.Trim(), StringComparison.OrdinalIgnoreCase));
                if (exists)
                    return Error("Профіль з такою назвою вже існує.");

                var profile = SqliteStorage.AddProfile(_sqlitePath, profileName.Trim());
                _manager.AddProfile(profile);
                _manager.SwitchProfile(profile.Id);
                LoadActiveProfileFromPrimaryStorage();
                return Ok(profile.Id);
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        public string SwitchProfile(string profileId)
        {
            try
            {
                bool switched = _manager.SwitchProfile(profileId);
                if (!switched) return Error("Профіль не знайдено.");

                LoadActiveProfileFromPrimaryStorage();
                return Ok(profileId);
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        public string GetActiveProfileId() => Ok(_manager.ActiveProfileId);

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
                if (!string.IsNullOrEmpty(err)) return Error(err);
                PersistActiveProfileToPrimaryStorage();
                return Ok(income.Id);
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
                if (!string.IsNullOrEmpty(err)) return Error(err);
                PersistActiveProfileToPrimaryStorage();
                return Ok();
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
                if (!string.IsNullOrEmpty(err)) return Error(err);
                PersistActiveProfileToPrimaryStorage();
                return Ok(expense.Id);
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
                if (!string.IsNullOrEmpty(err)) return Error(err);
                PersistActiveProfileToPrimaryStorage();
                return Ok();
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        // ===================== ВИДАЛЕННЯ =====================

        public string RemoveRecord(string id)
        {
            bool ok = _manager.RemoveRecord(id);
            if (ok) PersistActiveProfileToPrimaryStorage();
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
                if (!string.IsNullOrEmpty(err)) return Error(err);
                PersistActiveProfileToPrimaryStorage();
                return Ok(cat.Id);
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
                if (!string.IsNullOrEmpty(err)) return Error(err);
                PersistActiveProfileToPrimaryStorage();
                return Ok();
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        public string RemoveCategory(string id)
        {
            string err = _manager.RemoveCategory(id);
            if (string.IsNullOrEmpty(err)) PersistActiveProfileToPrimaryStorage();
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

        /// <summary>Експортує звіт у файл (діалог збереження). format = "json" | "xml" | "csv" | "xlsx".</summary>
        public string ExportReport(string reportJson, string format)
        {
            try
            {
                var report = JsonSerializer.Deserialize<Report>(reportJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (report == null) return Error("Некоректний звіт.");

                string ext = format.ToLowerInvariant() switch
                {
                    "xml" => "xml",
                    "csv" => "csv",
                    "xlsx" => "xlsx",
                    _ => "json"
                };

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

                if (ext == "xlsx")
                {
                    ExportReportToXlsx(report, filePath);
                }
                else
                {
                    string content = ext switch
                    {
                        "xml" => report.ExportToXML(),
                        "csv" => report.ExportToCSV(),
                        _ => report.ExportToJSON()
                    };
                    File.WriteAllText(filePath, content);
                }

                return Ok(filePath);
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        /// <summary>Фонове авто-збереження з ротацією резервних копій.</summary>
        public string SaveAutoBackup()
        {
            try
            {
                string dataPath = GetAutoSaveFilePath();
                string backupDir = GetBackupDirectoryPath();
                Directory.CreateDirectory(Path.GetDirectoryName(dataPath)!);
                Directory.CreateDirectory(backupDir);

                if (File.Exists(dataPath))
                {
                    string backupPath = Path.Combine(backupDir, $"autosave_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                    File.Copy(dataPath, backupPath, overwrite: true);
                    RotateBackups(backupDir, MaxAutoBackups);
                }

                DataStorage.SaveToJSON(dataPath,
                    _manager.Entrepreneur,
                    _manager.Incomes,
                    _manager.Expenses,
                    _manager.Categories,
                    _manager.Profiles,
                    _manager.ActiveProfileId);

                return Ok(dataPath);
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        /// <summary>Пробує завантажити останній autosave при старті застосунку.</summary>
        public bool TryLoadAutoSavedData()
        {
            try
            {
                string dataPath = GetAutoSaveFilePath();
                if (!File.Exists(dataPath)) return false;

                var (entrepreneur, incomes, expenses, categories, profiles, activeProfileId) = DataStorage.LoadFromJSON(dataPath);
                ApplyLoadedProfiles(profiles, activeProfileId);
                _manager.LoadData(incomes, expenses, categories, entrepreneur);
                PersistActiveProfileToPrimaryStorage();
                AppLogger.Info($"Autosave restored from: {dataPath}");
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to restore autosave", ex);
                return false;
            }
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
                        _manager.Categories,
                        _manager.Profiles,
                        _manager.ActiveProfileId);
                else
                    DataStorage.SaveToJSON(filePath,
                        _manager.Entrepreneur,
                        _manager.Incomes,
                        _manager.Expenses,
                        _manager.Categories,
                        _manager.Profiles,
                        _manager.ActiveProfileId);

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

                var (entrepreneur, incomes, expenses, categories, profiles, activeProfileId) = isXml
                    ? DataStorage.LoadFromXML(filePath)
                    : DataStorage.LoadFromJSON(filePath);

                ApplyLoadedProfiles(profiles, activeProfileId);
                _manager.LoadData(incomes, expenses, categories, entrepreneur);
                PersistActiveProfileToPrimaryStorage();
                return Ok("loaded");
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        // ===================== Утиліти =====================

        private static T? Deserialize<T>(string json) =>
            JsonSerializer.Deserialize<T>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        private void PersistActiveProfileToPrimaryStorage()
        {
            if (string.IsNullOrWhiteSpace(_manager.ActiveProfileId)) return;

            SqliteStorage.SaveProfileData(
                _sqlitePath,
                _manager.ActiveProfileId,
                _manager.Entrepreneur,
                _manager.Incomes,
                _manager.Expenses,
                _manager.Categories);
        }

        private void LoadActiveProfileFromPrimaryStorage()
        {
            if (string.IsNullOrWhiteSpace(_manager.ActiveProfileId)) return;

            var (entrepreneur, incomes, expenses, categories) =
                SqliteStorage.LoadProfileData(_sqlitePath, _manager.ActiveProfileId);

            _manager.LoadData(incomes, expenses, categories, entrepreneur);
        }

        private void ApplyLoadedProfiles(List<Profile> profiles, string activeProfileId)
        {
            if (profiles == null || profiles.Count == 0)
                return;

            SqliteStorage.UpsertProfiles(_sqlitePath, profiles);

            string resolvedActiveProfileId = ResolveActiveProfileId(profiles, activeProfileId);
            _manager.SetProfiles(profiles, resolvedActiveProfileId);
        }

        private static string ResolveActiveProfileId(List<Profile> profiles, string activeProfileId)
        {
            if (profiles == null || profiles.Count == 0)
                return activeProfileId ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(activeProfileId) &&
                profiles.Any(p => p.Id == activeProfileId))
            {
                return activeProfileId;
            }

            return profiles[0].Id;
        }

        private static void ExportReportToXlsx(Report report, string filePath)
        {
            using var workbook = new XLWorkbook();

            var summary = workbook.Worksheets.Add("Summary");
            summary.Cell(1, 1).Value = "StartDate";
            summary.Cell(1, 2).Value = report.StartDate.ToString("yyyy-MM-dd");
            summary.Cell(2, 1).Value = "EndDate";
            summary.Cell(2, 2).Value = report.EndDate.ToString("yyyy-MM-dd");
            summary.Cell(3, 1).Value = "TotalIncome";
            summary.Cell(3, 2).Value = report.TotalIncome;
            summary.Cell(4, 1).Value = "TotalExpense";
            summary.Cell(4, 2).Value = report.TotalExpense;
            summary.Cell(5, 1).Value = "NetProfit";
            summary.Cell(5, 2).Value = report.NetProfit;

            var incomesSheet = workbook.Worksheets.Add("Incomes");
            incomesSheet.Cell(1, 1).Value = "Date";
            incomesSheet.Cell(1, 2).Value = "Source";
            incomesSheet.Cell(1, 3).Value = "Amount";
            incomesSheet.Cell(1, 4).Value = "Description";

            int row = 2;
            foreach (var income in report.Incomes)
            {
                incomesSheet.Cell(row, 1).Value = income.Date.ToString("yyyy-MM-dd");
                incomesSheet.Cell(row, 2).Value = income.Source;
                incomesSheet.Cell(row, 3).Value = income.Amount;
                incomesSheet.Cell(row, 4).Value = income.Description;
                row++;
            }

            var expensesSheet = workbook.Worksheets.Add("Expenses");
            expensesSheet.Cell(1, 1).Value = "Date";
            expensesSheet.Cell(1, 2).Value = "Category";
            expensesSheet.Cell(1, 3).Value = "Amount";
            expensesSheet.Cell(1, 4).Value = "Description";

            row = 2;
            foreach (var expense in report.Expenses)
            {
                expensesSheet.Cell(row, 1).Value = expense.Date.ToString("yyyy-MM-dd");
                expensesSheet.Cell(row, 2).Value = expense.CategoryName;
                expensesSheet.Cell(row, 3).Value = expense.Amount;
                expensesSheet.Cell(row, 4).Value = expense.Description;
                row++;
            }

            summary.Columns().AdjustToContents();
            incomesSheet.Columns().AdjustToContents();
            expensesSheet.Columns().AdjustToContents();

            workbook.SaveAs(filePath);
        }

        private static string GetAutoSaveFilePath()
        {
            string baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FopFinance",
                "data");
            return Path.Combine(baseDir, "autosave.json");
        }

        private static string GetBackupDirectoryPath()
        {
            string baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FopFinance",
                "data",
                "backups");
            return baseDir;
        }

        private static void RotateBackups(string backupDir, int keepCount)
        {
            var backups = new DirectoryInfo(backupDir)
                .GetFiles("autosave_*.json")
                .OrderByDescending(f => f.CreationTimeUtc)
                .ToList();

            for (int i = keepCount; i < backups.Count; i++)
            {
                backups[i].Delete();
            }
        }

        /// <summary>Формує JSON-відповідь успіху через серіалізацію DTO.</summary>
        private static string Ok(string data = "") =>
            JsonSerializer.Serialize(new BridgeResponse { Ok = true, Data = data }, _jsonOpts);

        /// <summary>Формує JSON-відповідь помилки через серіалізацію DTO.</summary>
        private static string Error(string msg) =>
            JsonSerializer.Serialize(new BridgeResponse { Ok = false, Error = msg }, _jsonOpts);
    }
}
