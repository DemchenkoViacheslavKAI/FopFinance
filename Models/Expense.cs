using System;

namespace FopFinance.Models
{
    /// <summary>
    /// Клас «Категорія витрат».
    /// Дозволяє класифікувати витрати за типом (оренда, реклама тощо).
    /// </summary>
    public class Category
    {
        /// <summary>Унікальний ідентифікатор категорії.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Назва категорії (обов'язкове поле).</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Опис категорії (необов'язково).</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Оновлює назву та опис категорії.</summary>
        public void Edit(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }

    /// <summary>
    /// Клас «Витрата» — успадковує FinancialRecord.
    /// Представляє одну видаткову операцію ФОП.
    /// </summary>
    public class Expense : FinancialRecord
    {
        /// <summary>Ідентифікатор прив'язаної категорії.</summary>
        public string CategoryId { get; set; } = string.Empty;

        /// <summary>Назва категорії (денормалізовано для зручності відображення).</summary>
        public string CategoryName { get; set; } = string.Empty;

        /// <summary>Прив'язує витрату до категорії.</summary>
        public void AssignCategory(Category category)
        {
            if (category == null) throw new ArgumentNullException(nameof(category));
            CategoryId = category.Id;
            CategoryName = category.Name;
        }
    }
}
