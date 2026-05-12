using System;
using System.Collections.Generic;
using System.Linq;
using FopFinance.Models;

namespace FopFinance.Repositories
{
    public class IncomeRepository : IIncomeRepository
    {
        private readonly List<Income> _items = new();

        public IReadOnlyList<Income> GetAll() => _items.AsReadOnly();
        public Income? FindById(string id) => _items.FirstOrDefault(i => i.Id == id);

        public void Add(Income entity) => _items.Add(entity);

        public bool Remove(string id) => _items.RemoveAll(i => i.Id == id) > 0;

        public bool Update(Income updated)
        {
            int idx = _items.FindIndex(i => i.Id == updated.Id);
            if (idx < 0) return false;
            _items[idx] = updated;
            return true;
        }

        public void ReplaceAll(IEnumerable<Income> entities)
        {
            _items.Clear();
            _items.AddRange(entities ?? Array.Empty<Income>());
        }
    }

    public class ExpenseRepository : IExpenseRepository
    {
        private readonly List<Expense> _items = new();

        public IReadOnlyList<Expense> GetAll() => _items.AsReadOnly();
        public Expense? FindById(string id) => _items.FirstOrDefault(e => e.Id == id);

        public void Add(Expense entity) => _items.Add(entity);

        public bool Remove(string id) => _items.RemoveAll(e => e.Id == id) > 0;

        public bool Update(Expense updated)
        {
            int idx = _items.FindIndex(e => e.Id == updated.Id);
            if (idx < 0) return false;
            _items[idx] = updated;
            return true;
        }

        public bool AnyWithCategory(string categoryId) =>
            _items.Any(e => e.CategoryId == categoryId);

        public void ReplaceAll(IEnumerable<Expense> entities)
        {
            _items.Clear();
            _items.AddRange(entities ?? Array.Empty<Expense>());
        }
    }

    public class CategoryRepository : ICategoryRepository
    {
        private readonly List<Category> _items = new();

        public IReadOnlyList<Category> GetAll() => _items.AsReadOnly();
        public Category? FindById(string id) => _items.FirstOrDefault(c => c.Id == id);

        public void Add(Category entity) => _items.Add(entity);

        public bool Remove(string id) => _items.RemoveAll(c => c.Id == id) > 0;

        public bool Update(Category updated)
        {
            int idx = _items.FindIndex(c => c.Id == updated.Id);
            if (idx < 0) return false;
            _items[idx].Edit(updated.Name, updated.Description);
            return true;
        }

        public bool ExistsByName(string name, string? excludeId = null) =>
            _items.Any(c =>
                c.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                c.Id != excludeId);

        public void ReplaceAll(IEnumerable<Category> entities)
        {
            _items.Clear();
            _items.AddRange(entities ?? Array.Empty<Category>());
        }
    }

    public class EntrepreneurRepository : IEntrepreneurRepository
    {
        private Entrepreneur _entrepreneur = new();

        public Entrepreneur Get() => _entrepreneur;
        public void Set(Entrepreneur entrepreneur) => _entrepreneur = entrepreneur ?? new Entrepreneur();
    }

    public class ProfileRepository : IProfileRepository
    {
        private readonly List<Profile> _profiles = new();

        public IReadOnlyList<Profile> GetAll() => _profiles.AsReadOnly();

        public void Add(Profile profile) => _profiles.Add(profile);

        public bool Exists(string profileId) => _profiles.Any(p => p.Id == profileId);

        // IRepository<Profile> not inherited by IProfileRepository intentionally —
        // profiles are managed via ProfileManager/Facade, not generic CRUD.
        // ReplaceAll is exposed here for internal use by ProfileManager.
        public void ReplaceAll(IEnumerable<Profile> profiles)
        {
            _profiles.Clear();
            _profiles.AddRange(profiles ?? Array.Empty<Profile>());
        }
    }
}
