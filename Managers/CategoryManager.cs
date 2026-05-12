using System.Collections.Generic;
using FopFinance.Models;
using FopFinance.Repositories;

namespace FopFinance.Managers
{
    /// <summary>
    /// Handles all business logic for expense categories.
    /// </summary>
    public class CategoryManager
    {
        private readonly ICategoryRepository _repository;
        private readonly IExpenseRepository _expenses;

        public CategoryManager(ICategoryRepository repository, IExpenseRepository expenses)
        {
            _repository = repository;
            _expenses = expenses;
        }

        public IReadOnlyList<Category> GetAll() => _repository.GetAll();

        public string Add(Category category)
        {
            if (string.IsNullOrWhiteSpace(category.Name))
                return "Назва категорії є обов'язковою.";

            if (_repository.ExistsByName(category.Name))
                return "Категорія з такою назвою вже існує.";

            _repository.Add(category);
            return string.Empty;
        }

        public string Update(Category updated)
        {
            if (string.IsNullOrWhiteSpace(updated.Name))
                return "Назва категорії є обов'язковою.";

            return _repository.Update(updated)
                ? string.Empty
                : "Категорію не знайдено.";
        }

        public string Remove(string id)
        {
            if (_expenses.AnyWithCategory(id))
                return "Не можна видалити категорію, що використовується у витратах.";

            return _repository.Remove(id)
                ? string.Empty
                : "Категорію не знайдено.";
        }
    }
}
