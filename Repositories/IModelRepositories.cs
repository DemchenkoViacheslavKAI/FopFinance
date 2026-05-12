using System.Collections.Generic;
using FopFinance.Models;

namespace FopFinance.Repositories
{
    public interface IIncomeRepository : IRepository<Income>
    {
        Income? FindById(string id);
        bool Update(Income updated);
    }

    public interface IExpenseRepository : IRepository<Expense>
    {
        Expense? FindById(string id);
        bool Update(Expense updated);
        bool AnyWithCategory(string categoryId);
    }

    public interface ICategoryRepository : IRepository<Category>
    {
        Category? FindById(string id);
        bool Update(Category updated);
        bool ExistsByName(string name, string? excludeId = null);
    }

    public interface IEntrepreneurRepository
    {
        Entrepreneur Get();
        void Set(Entrepreneur entrepreneur);
    }

    public interface IProfileRepository
    {
        IReadOnlyList<Profile> GetAll();
        void Add(Profile profile);
        bool Exists(string profileId);
        void ReplaceAll(IEnumerable<Profile> profiles);
    }
}
