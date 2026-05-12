using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FopFinance.Facade;
using FopFinance.Models;
using FopFinance.Storage;
using FopFinance.UnitOfWork;

namespace FopFinance.Services
{
    /// <summary>
    /// Handles autosave, manual save/load to JSON/XML files, and backup rotation.
    /// DataStorage (JSON/XML) is only used here — for portable file exchange.
    /// The primary store remains SQLite via FinanceStorageFacade.
    /// </summary>
    public class BackupService
    {
        private const int MaxAutoBackups = 10;

        private readonly IUnitOfWork _unitOfWork;
        private readonly FinanceStorageFacade _storage;
        private readonly Form _mainForm;

        private static string AutoSavePath =>
            Path.Combine(BaseDataDir, "autosave.json");

        private static string BackupDir =>
            Path.Combine(BaseDataDir, "backups");

        private static string BaseDataDir =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FopFinance", "data");

        public BackupService(IUnitOfWork unitOfWork, FinanceStorageFacade storage, Form mainForm)
        {
            _unitOfWork = unitOfWork;
            _storage    = storage;
            _mainForm   = mainForm;
        }

        // ─── Autosave ─────────────────────────────────────────────────────────

        public string SaveAutoBackup()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AutoSavePath)!);
                Directory.CreateDirectory(BackupDir);

                if (File.Exists(AutoSavePath))
                {
                    string dest = Path.Combine(BackupDir, $"autosave_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                    File.Copy(AutoSavePath, dest, overwrite: true);
                    RotateBackups(BackupDir, MaxAutoBackups);
                }

                DataStorage.SaveToJSON(
                    AutoSavePath,
                    _unitOfWork.Entrepreneur.Get(),
                    new List<Income>(_unitOfWork.Incomes.GetAll()),
                    new List<Expense>(_unitOfWork.Expenses.GetAll()),
                    new List<Category>(_unitOfWork.Categories.GetAll()),
                    new List<Profile>(_unitOfWork.Profiles.GetAll()),
                    _unitOfWork.ActiveProfileId);

                return BridgeHelpers.Ok(AutoSavePath);
            }
            catch (Exception ex) { return BridgeHelpers.Error(ex.Message); }
        }

        public bool TryRestoreAutoSave(Action<List<Profile>, string, List<Income>, List<Expense>, List<Category>, Entrepreneur> applyCallback)
        {
            try
            {
                if (!File.Exists(AutoSavePath)) return false;

                var (entrepreneur, incomes, expenses, categories, profiles, activeProfileId) =
                    DataStorage.LoadFromJSON(AutoSavePath);

                applyCallback(profiles, activeProfileId, incomes, expenses, categories, entrepreneur);
                AppLogger.Info($"Autosave restored from: {AutoSavePath}");
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to restore autosave", ex);
                return false;
            }
        }

        // ─── Manual Save / Load ───────────────────────────────────────────────

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

            if (filePath == null) return BridgeHelpers.Ok("cancelled");

            try
            {
                var entrepreneur = _unitOfWork.Entrepreneur.Get();
                var incomes      = new List<Income>(_unitOfWork.Incomes.GetAll());
                var expenses     = new List<Expense>(_unitOfWork.Expenses.GetAll());
                var categories   = new List<Category>(_unitOfWork.Categories.GetAll());
                var profiles     = new List<Profile>(_unitOfWork.Profiles.GetAll());

                if (format == "xml")
                    DataStorage.SaveToXML(filePath, entrepreneur, incomes, expenses, categories, profiles, _unitOfWork.ActiveProfileId);
                else
                    DataStorage.SaveToJSON(filePath, entrepreneur, incomes, expenses, categories, profiles, _unitOfWork.ActiveProfileId);

                return BridgeHelpers.Ok(filePath);
            }
            catch (Exception ex) { return BridgeHelpers.Error(ex.Message); }
        }

        public string LoadData(Action<List<Profile>, string, List<Income>, List<Expense>, List<Category>, Entrepreneur> applyCallback)
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

            if (filePath == null) return BridgeHelpers.Ok("cancelled");

            try
            {
                bool isXml = filePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);
                var (entrepreneur, incomes, expenses, categories, profiles, activeProfileId) = isXml
                    ? DataStorage.LoadFromXML(filePath)
                    : DataStorage.LoadFromJSON(filePath);

                applyCallback(profiles, activeProfileId, incomes, expenses, categories, entrepreneur);
                return BridgeHelpers.Ok("loaded");
            }
            catch (Exception ex) { return BridgeHelpers.Error(ex.Message); }
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private static void RotateBackups(string dir, int keep)
        {
            var files = new DirectoryInfo(dir)
                .GetFiles("autosave_*.json")
                .OrderByDescending(f => f.CreationTimeUtc)
                .ToList();

            for (int i = keep; i < files.Count; i++)
                files[i].Delete();
        }
    }
}
