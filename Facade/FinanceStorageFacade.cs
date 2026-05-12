using System.Collections.Generic;
using FopFinance.Models;
using FopFinance.Storage;

namespace FopFinance.Facade
{
    /// <summary>
    /// Facade over SqliteStorage.
    /// Provides a clear, domain-oriented API so callers never deal with raw SQL concerns.
    /// All path management is internal; callers only work with domain objects.
    /// </summary>
    public class FinanceStorageFacade
    {
        private readonly string _dbPath;

        public FinanceStorageFacade(string dbPath)
        {
            _dbPath = dbPath;
            SqliteStorage.EnsureDatabase(_dbPath);
        }

        // ─── Profiles ────────────────────────────────────────────────────────

        public List<Profile> GetProfiles() =>
            SqliteStorage.GetProfiles(_dbPath);

        public Profile CreateProfile(string name) =>
            SqliteStorage.AddProfile(_dbPath, name);

        public void UpsertProfiles(IEnumerable<Profile> profiles) =>
            SqliteStorage.UpsertProfiles(_dbPath, profiles);

        // ─── Profile Data ─────────────────────────────────────────────────────

        public (Entrepreneur entrepreneur, List<Income> incomes, List<Expense> expenses, List<Category> categories)
            LoadProfileData(string profileId) =>
            SqliteStorage.LoadProfileData(_dbPath, profileId);

        public void SaveProfileData(
            string profileId,
            Entrepreneur entrepreneur,
            List<Income> incomes,
            List<Expense> expenses,
            List<Category> categories) =>
            SqliteStorage.SaveProfileData(_dbPath, profileId, entrepreneur, incomes, expenses, categories);
    }
}
