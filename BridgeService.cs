using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FopFinance.Facade;
using FopFinance.Managers;
using FopFinance.Models;
using FopFinance.Repositories;
using FopFinance.Services;
using FopFinance.UnitOfWork;

namespace FopFinance
{
    /// <summary>
    /// COM-visible bridge between JavaScript (WebView2) and C#.
    /// Acts as a thin coordinator: it owns no logic itself — all calls
    /// are delegated to focused services that each have a single responsibility.
    ///
    /// Initialisation order:
    ///   1. FinanceStorageFacade  — wraps SQLite, no raw SQL outside it
    ///   2. SqliteUnitOfWork      — coordinates all in-memory repositories
    ///   3. Focused Managers      — pure business logic, no I/O
    ///   4. Focused Bridge Services — JSON serialisation + bridge protocol
    /// </summary>
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class BridgeService
    {
        // Focused services (each owns one concern)
        private readonly EntrepreneurBridgeService _entrepreneurService;
        private readonly ProfileBridgeService      _profileService;
        private readonly IncomeBridgeService       _incomeService;
        private readonly ExpenseBridgeService      _expenseService;
        private readonly CategoryBridgeService     _categoryService;
        private readonly ReportBridgeService       _reportService;
        private readonly BackupService             _backupService;

        // Cross-cutting dependencies shared by services
        private readonly IUnitOfWork          _unitOfWork;
        private readonly FinanceStorageFacade _storage;
        private readonly ProfileManager       _profileManager;

        public BridgeService(Form mainForm)
        {
            // 1. Storage facade (single path to SQLite, no raw SQL outside it)
            _storage = new FinanceStorageFacade(AppPaths.DatabasePath);

            // 2. Repositories + Unit of Work
            _unitOfWork = new SqliteUnitOfWork(_storage);

            // 3. Focused managers (pure business logic, no I/O)
            var incomeManager   = new IncomeManager(_unitOfWork.Incomes);
            var expenseManager  = new ExpenseManager(_unitOfWork.Expenses, _unitOfWork.Categories);
            var categoryManager = new CategoryManager(_unitOfWork.Categories, _unitOfWork.Expenses);
            _profileManager     = new ProfileManager(_unitOfWork.Profiles, _storage, _unitOfWork);
            var reportManager   = new ReportManager(incomeManager, expenseManager);

            // 4. Bridge services (JSON serialisation + bridge protocol only)
            _entrepreneurService = new EntrepreneurBridgeService(_unitOfWork.Entrepreneur, _unitOfWork);
            _profileService      = new ProfileBridgeService(_profileManager, _unitOfWork);
            _incomeService       = new IncomeBridgeService(incomeManager, _unitOfWork);
            _expenseService      = new ExpenseBridgeService(expenseManager, _unitOfWork);
            _categoryService     = new CategoryBridgeService(categoryManager, _unitOfWork);
            _reportService       = new ReportBridgeService(reportManager, mainForm);
            _backupService       = new BackupService(_unitOfWork, _storage, mainForm);

            // 5. Bootstrap profiles from SQLite
            var profiles = _storage.GetProfiles();
            if (profiles.Count == 0)
                profiles.Add(_storage.CreateProfile("Основний профіль"));

            _profileManager.Initialize(profiles, profiles[0].Id);
            _unitOfWork.SetActiveProfile(_profileManager.ActiveProfileId);
            _unitOfWork.Reload();

            AppLogger.Info("BridgeService created");
        }

        // Utility
        public string Ping() => BridgeHelpers.Ok("pong");

        public string LogClient(string message)
        {
            AppLogger.Info($"Frontend: {message}");
            return BridgeHelpers.Ok();
        }

        // Entrepreneur
        public string GetEntrepreneur()            => _entrepreneurService.GetEntrepreneur();
        public string SaveEntrepreneur(string json) => _entrepreneurService.SaveEntrepreneur(json);

        // Profiles
        public string GetProfiles()            => _profileService.GetProfiles();
        public string GetActiveProfileId()     => _profileService.GetActiveProfileId();
        public string AddProfile(string name)  => _profileService.AddProfile(name);
        public string SwitchProfile(string id) => _profileService.SwitchProfile(id);

        // Incomes
        public string GetIncomes()              => _incomeService.GetIncomes();
        public string AddIncome(string json)    => _incomeService.AddIncome(json);
        public string UpdateIncome(string json) => _incomeService.UpdateIncome(json);

        // Expenses
        public string GetExpenses()              => _expenseService.GetExpenses();
        public string AddExpense(string json)    => _expenseService.AddExpense(json);
        public string UpdateExpense(string json) => _expenseService.UpdateExpense(json);

        /// <summary>Removes an income or expense record by id.</summary>
        public string RemoveRecord(string id)
        {
            string result = _incomeService.RemoveIncome(id);
            // If income removal failed (not found), try expense
            if (result.Contains("\"ok\":false"))
                result = _expenseService.RemoveExpense(id);
            return result;
        }

        // Categories
        public string GetCategories()             => _categoryService.GetCategories();
        public string AddCategory(string json)    => _categoryService.AddCategory(json);
        public string UpdateCategory(string json) => _categoryService.UpdateCategory(json);
        public string RemoveCategory(string id)   => _categoryService.RemoveCategory(id);

        // Reports
        public string GenerateReport(string startIso, string endIso) =>
            _reportService.GenerateReport(startIso, endIso);

        public string ExportReport(string reportJson, string format) =>
            _reportService.ExportReport(reportJson, format);

        // Backup / Save / Load
        public string SaveAutoBackup()       => _backupService.SaveAutoBackup();
        public string SaveData(string format) => _backupService.SaveData(format);
        public string LoadData()              => _backupService.LoadData(ApplyLoadedData);

        public bool TryLoadAutoSavedData() =>
            _backupService.TryRestoreAutoSave(ApplyLoadedData);

        // Callback passed to BackupService when restoring data from file/autosave
        private void ApplyLoadedData(
            List<Profile> profiles,
            string activeProfileId,
            List<Income> incomes,
            List<Expense> expenses,
            List<Category> categories,
            Entrepreneur entrepreneur)
        {
            _storage.UpsertProfiles(profiles);
            _profileManager.Initialize(profiles, activeProfileId);
            _unitOfWork.LoadAndCommit(entrepreneur, incomes, expenses, categories);
        }
    }
}
