using FopFinance.Models;
using FopFinance.Repositories;
using System.Collections.Generic;

namespace FopFinance.UnitOfWork
{
    /// <summary>
    /// Aggregates all repositories under a single boundary.
    /// Commit() persists the in-memory state to the primary SQLite store.
    /// </summary>
    public interface IUnitOfWork
    {
        IIncomeRepository Incomes { get; }
        IExpenseRepository Expenses { get; }
        ICategoryRepository Categories { get; }
        IEntrepreneurRepository Entrepreneur { get; }
        IProfileRepository Profiles { get; }

        string ActiveProfileId { get; }
        void SetActiveProfile(string profileId);

        /// <summary>Persists all in-memory data for the active profile to SQLite.</summary>
        void Commit();

        /// <summary>Reloads data for the active profile from SQLite.</summary>
        void Reload();

        /// <summary>Replaces in-memory state with the provided data and commits.</summary>
        void LoadAndCommit(
            Entrepreneur entrepreneur,
            List<Income> incomes,
            List<Expense> expenses,
            List<Category> categories);
    }
}
