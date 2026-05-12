using System.Collections.Generic;
using FopFinance.Facade;
using FopFinance.Models;
using FopFinance.Repositories;

namespace FopFinance.UnitOfWork
{
    /// <summary>
    /// SQLite-backed Unit of Work.
    /// Coordinates in-memory repositories with the FinanceStorageFacade.
    /// </summary>
    public class SqliteUnitOfWork : IUnitOfWork
    {
        private readonly FinanceStorageFacade _storage;

        public IIncomeRepository Incomes { get; } = new IncomeRepository();
        public IExpenseRepository Expenses { get; } = new ExpenseRepository();
        public ICategoryRepository Categories { get; } = new CategoryRepository();
        public IEntrepreneurRepository Entrepreneur { get; } = new EntrepreneurRepository();
        public IProfileRepository Profiles { get; } = new ProfileRepository();

        public string ActiveProfileId { get; private set; } = string.Empty;

        public SqliteUnitOfWork(FinanceStorageFacade storage)
        {
            _storage = storage;
        }

        public void SetActiveProfile(string profileId)
        {
            ActiveProfileId = profileId;
        }

        public void Commit()
        {
            if (string.IsNullOrWhiteSpace(ActiveProfileId)) return;

            _storage.SaveProfileData(
                ActiveProfileId,
                Entrepreneur.Get(),
                new List<Income>(Incomes.GetAll()),
                new List<Expense>(Expenses.GetAll()),
                new List<Category>(Categories.GetAll()));
        }

        public void Reload()
        {
            if (string.IsNullOrWhiteSpace(ActiveProfileId)) return;

            var (entrepreneur, incomes, expenses, categories) =
                _storage.LoadProfileData(ActiveProfileId);

            Entrepreneur.Set(entrepreneur);
            Incomes.ReplaceAll(incomes);
            Expenses.ReplaceAll(expenses);
            Categories.ReplaceAll(categories);
        }

        public void LoadAndCommit(
            Entrepreneur entrepreneur,
            List<Income> incomes,
            List<Expense> expenses,
            List<Category> categories)
        {
            Entrepreneur.Set(entrepreneur);
            Incomes.ReplaceAll(incomes);
            Expenses.ReplaceAll(expenses);
            Categories.ReplaceAll(categories);
            Commit();
        }
    }
}
