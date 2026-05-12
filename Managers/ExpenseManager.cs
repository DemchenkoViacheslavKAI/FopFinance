using System;
using System.Collections.Generic;
using System.Linq;
using FopFinance.Models;
using FopFinance.Repositories;
using FopFinance.Validators;

namespace FopFinance.Managers
{
    /// <summary>
    /// Handles all business logic for expense records.
    /// </summary>
    public class ExpenseManager
    {
        private readonly IExpenseRepository _repository;
        private readonly ICategoryRepository _categories;

        public ExpenseManager(IExpenseRepository repository, ICategoryRepository categories)
        {
            _repository = repository;
            _categories = categories;
        }

        public IReadOnlyList<Expense> GetAll() => _repository.GetAll();

        public string Add(Expense expense)
        {
            string error = ExpenseValidator.Validate(expense);
            if (!string.IsNullOrEmpty(error)) return error;

            var category = _categories.FindById(expense.CategoryId);
            if (category == null) return "Вибрана категорія не існує.";

            expense.CategoryName = category.Name;
            _repository.Add(expense);
            return string.Empty;
        }

        public string Update(Expense updated)
        {
            string error = ExpenseValidator.Validate(updated);
            if (!string.IsNullOrEmpty(error)) return error;

            var category = _categories.FindById(updated.CategoryId);
            if (category == null) return "Вибрана категорія не існує.";

            updated.CategoryName = category.Name;
            return _repository.Update(updated)
                ? string.Empty
                : "Запис не знайдено.";
        }

        public bool Remove(string id) => _repository.Remove(id);

        public decimal CalculateTotal(DateTime? from = null, DateTime? to = null)
        {
            var items = from.HasValue
                ? _repository.GetAll().Where(e => e.Date >= from && e.Date <= to)
                : _repository.GetAll();
            return items.Sum(e => e.Amount);
        }

        public List<Expense> GetByPeriod(DateTime start, DateTime end) =>
            _repository.GetAll()
                .Where(e => e.Date >= start && e.Date <= end)
                .ToList();
    }
}
