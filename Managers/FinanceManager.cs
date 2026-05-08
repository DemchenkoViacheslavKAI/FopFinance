using System;
using System.Collections.Generic;
using System.Linq;
using FopFinance.Models;

namespace FopFinance.Managers
{
    /// <summary>
    /// Центральний менеджер фінансових операцій.
    /// Зберігає списки доходів, витрат і категорій;
    /// реалізує всю бізнес-логіку CRUD та агрегацій.
    /// </summary>
    public class FinanceManager
    {
        // --- Колекції даних ---
        public List<Income>   Incomes    { get; private set; } = new();
        public List<Expense>  Expenses   { get; private set; } = new();
        public List<Category> Categories { get; private set; } = new();
        public List<Profile> Profiles { get; private set; } = new();
        public string ActiveProfileId { get; private set; } = string.Empty;

        /// <summary>Дані підприємця (профіль ФОП).</summary>
        public Entrepreneur Entrepreneur { get; set; } = new();

        public void SetProfiles(List<Profile> profiles, string activeProfileId)
        {
            Profiles = profiles ?? new List<Profile>();
            ActiveProfileId = activeProfileId ?? string.Empty;
        }

        public void AddProfile(Profile profile)
        {
            Profiles.Add(profile);
            if (string.IsNullOrEmpty(ActiveProfileId)) ActiveProfileId = profile.Id;
        }

        public bool SwitchProfile(string profileId)
        {
            bool exists = Profiles.Any(p => p.Id == profileId);
            if (!exists) return false;
            ActiveProfileId = profileId;
            return true;
        }

        // ===================== ДОХОДИ =====================

        /// <summary>Додає дохід після валідації. Повертає помилку або порожній рядок.</summary>
        public string AddIncome(Income income)
        {
            string error = income.Validate();
            if (!string.IsNullOrEmpty(error)) return error;

            Incomes.Add(income);
            return string.Empty;
        }

        /// <summary>Оновлює існуючий дохід за Id.</summary>
        public string UpdateIncome(Income updated)
        {
            string error = updated.Validate();
            if (!string.IsNullOrEmpty(error)) return error;

            int idx = Incomes.FindIndex(i => i.Id == updated.Id);
            if (idx < 0) return "Запис не знайдено.";

            Incomes[idx] = updated;
            return string.Empty;
        }

        // ===================== ВИТРАТИ =====================

        /// <summary>Додає витрату після валідації.</summary>
        public string AddExpense(Expense expense)
        {
            string error = expense.Validate();
            if (!string.IsNullOrEmpty(error)) return error;

            var category = Categories.FirstOrDefault(c => c.Id == expense.CategoryId);
            if (category == null)
                return "Вибрана категорія не існує.";

            expense.CategoryName = category.Name;

            Expenses.Add(expense);
            return string.Empty;
        }

        /// <summary>Оновлює існуючу витрату за Id.</summary>
        public string UpdateExpense(Expense updated)
        {
            string error = updated.Validate();
            if (!string.IsNullOrEmpty(error)) return error;

            var category = Categories.FirstOrDefault(c => c.Id == updated.CategoryId);
            if (category == null)
                return "Вибрана категорія не існує.";

            updated.CategoryName = category.Name;

            int idx = Expenses.FindIndex(e => e.Id == updated.Id);
            if (idx < 0) return "Запис не знайдено.";

            Expenses[idx] = updated;
            return string.Empty;
        }

        // ===================== ВИДАЛЕННЯ =====================

        /// <summary>Видаляє будь-який запис (дохід або витрату) за Id.</summary>
        public bool RemoveRecord(string id)
        {
            int i = Incomes.RemoveAll(x => x.Id == id);
            if (i > 0) return true;

            int e = Expenses.RemoveAll(x => x.Id == id);
            return e > 0;
        }

        // ===================== КАТЕГОРІЇ =====================

        /// <summary>Додає категорію (назва обов'язкова).</summary>
        public string AddCategory(Category category)
        {
            if (string.IsNullOrWhiteSpace(category.Name))
                return "Назва категорії є обов'язковою.";

            if (Categories.Any(c => c.Name.Equals(category.Name, StringComparison.OrdinalIgnoreCase)))
                return "Категорія з такою назвою вже існує.";

            Categories.Add(category);
            return string.Empty;
        }

        /// <summary>Оновлює категорію.</summary>
        public string UpdateCategory(Category updated)
        {
            if (string.IsNullOrWhiteSpace(updated.Name))
                return "Назва категорії є обов'язковою.";

            int idx = Categories.FindIndex(c => c.Id == updated.Id);
            if (idx < 0) return "Категорію не знайдено.";

            Categories[idx].Edit(updated.Name, updated.Description);
            return string.Empty;
        }

        /// <summary>Видаляє категорію (якщо немає прив'язаних витрат).</summary>
        public string RemoveCategory(string id)
        {
            bool inUse = Expenses.Any(e => e.CategoryId == id);
            if (inUse) return "Не можна видалити категорію, що використовується у витратах.";

            int removed = Categories.RemoveAll(c => c.Id == id);
            return removed > 0 ? string.Empty : "Категорію не знайдено.";
        }

        // ===================== ФІЛЬТРАЦІЯ =====================

        /// <summary>Повертає записи за заданий період (включно).</summary>
        public (List<Income> incomes, List<Expense> expenses) GetRecordsByPeriod(
            DateTime start, DateTime end)
        {
            var incomes  = Incomes .Where(i => i.Date >= start && i.Date <= end).ToList();
            var expenses = Expenses.Where(e => e.Date >= start && e.Date <= end).ToList();
            return (incomes, expenses);
        }

        // ===================== АГРЕГАЦІЇ =====================

        public decimal CalculateTotalIncome(DateTime? from = null, DateTime? to = null)
        {
            var list = from.HasValue
                ? Incomes.Where(i => i.Date >= from && i.Date <= to)
                : Incomes.AsEnumerable();
            return list.Sum(i => i.Amount);
        }

        public decimal CalculateTotalExpense(DateTime? from = null, DateTime? to = null)
        {
            var list = from.HasValue
                ? Expenses.Where(e => e.Date >= from && e.Date <= to)
                : Expenses.AsEnumerable();
            return list.Sum(e => e.Amount);
        }

        public decimal CalculateNetProfit(DateTime? from = null, DateTime? to = null)
        {
            return CalculateTotalIncome(from, to) - CalculateTotalExpense(from, to);
        }

        // ===================== ЗВІТ =====================

        /// <summary>Формує об'єкт звіту за заданий період.</summary>
        public Report GenerateReport(DateTime start, DateTime end)
        {
            var (incomes, expenses) = GetRecordsByPeriod(start, end);

            return new Report
            {
                StartDate    = start,
                EndDate      = end,
                TotalIncome  = incomes .Sum(i => i.Amount),
                TotalExpense = expenses.Sum(e => e.Amount),
                Incomes      = incomes,
                Expenses     = expenses
            };
        }

        // ===================== ЗАВАНТАЖЕННЯ ДАНИХ =====================

        /// <summary>Замінює поточні колекції завантаженими з файлу.</summary>
        public void LoadData(List<Income> incomes, List<Expense> expenses,
                             List<Category> categories, Entrepreneur? entrepreneur = null)
        {
            Incomes    = incomes    ?? new();
            Expenses   = expenses   ?? new();
            Categories = categories ?? new();
            if (entrepreneur != null) Entrepreneur = entrepreneur;
        }
    }
}
