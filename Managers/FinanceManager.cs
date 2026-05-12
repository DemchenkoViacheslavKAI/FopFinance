using System;
using System.Collections.Generic;
using System.Linq;
using FopFinance.Models;
using FopFinance.Validators;

namespace FopFinance.Managers
{
    /// <summary>
    /// In-memory finance manager used by tests and simple scenarios.
    /// </summary>
    public class FinanceManager
    {
        public List<Income> Incomes { get; private set; } = new();
        public List<Expense> Expenses { get; private set; } = new();
        public List<Category> Categories { get; private set; } = new();
        public List<Profile> Profiles { get; private set; } = new();
        public string ActiveProfileId { get; private set; } = string.Empty;
        public Entrepreneur Entrepreneur { get; private set; } = new();

        public string AddIncome(Income income)
        {
            string error = IncomeValidator.Validate(income);
            if (!string.IsNullOrEmpty(error)) return error;

            Incomes.Add(income);
            return string.Empty;
        }

        public string UpdateIncome(Income updated)
        {
            string error = IncomeValidator.Validate(updated);
            if (!string.IsNullOrEmpty(error)) return error;

            int index = Incomes.FindIndex(i => i.Id == updated.Id);
            if (index < 0) return "Запис не знайдено.";

            Incomes[index] = updated;
            return string.Empty;
        }

        public string AddExpense(Expense expense)
        {
            string error = ExpenseValidator.Validate(expense);
            if (!string.IsNullOrEmpty(error)) return error;

            var category = Categories.Find(c => c.Id == expense.CategoryId);
            if (category == null) return "Вибрана категорія не існує.";

            expense.CategoryName = category.Name;
            Expenses.Add(expense);
            return string.Empty;
        }

        public string UpdateExpense(Expense updated)
        {
            string error = ExpenseValidator.Validate(updated);
            if (!string.IsNullOrEmpty(error)) return error;

            var category = Categories.Find(c => c.Id == updated.CategoryId);
            if (category == null) return "Вибрана категорія не існує.";

            int index = Expenses.FindIndex(e => e.Id == updated.Id);
            if (index < 0) return "Запис не знайдено.";

            updated.CategoryName = category.Name;
            Expenses[index] = updated;
            return string.Empty;
        }

        public bool RemoveRecord(string id)
        {
            int incomeIndex = Incomes.FindIndex(i => i.Id == id);
            if (incomeIndex >= 0)
            {
                Incomes.RemoveAt(incomeIndex);
                return true;
            }

            int expenseIndex = Expenses.FindIndex(e => e.Id == id);
            if (expenseIndex >= 0)
            {
                Expenses.RemoveAt(expenseIndex);
                return true;
            }

            return false;
        }

        public string AddCategory(Category category)
        {
            string name = (category.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                return "Назва категорії є обов'язковою.";

            foreach (var existing in Categories)
            {
                if (existing.Name.Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                    return "Категорія з такою назвою вже існує.";
            }

            category.Name = name;
            Categories.Add(category);
            return string.Empty;
        }

        public string UpdateCategory(Category updated)
        {
            string name = (updated.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                return "Назва категорії є обов'язковою.";

            int index = Categories.FindIndex(c => c.Id == updated.Id);
            if (index < 0) return "Категорію не знайдено.";

            Categories[index].Name = name;
            Categories[index].Description = updated.Description;
            return string.Empty;
        }

        public string RemoveCategory(string id)
        {
            foreach (var expense in Expenses)
                if (expense.CategoryId == id)
                    return "Не можна видалити категорію, що використовується у витратах.";

            int index = Categories.FindIndex(c => c.Id == id);
            if (index < 0) return "Категорію не знайдено.";

            Categories.RemoveAt(index);
            return string.Empty;
        }

        public (List<Income> incomes, List<Expense> expenses) GetRecordsByPeriod(DateTime start, DateTime end)
        {
            var incomes = Incomes
                .Where(i => i.Date >= start && i.Date <= end)
                .ToList();
            var expenses = Expenses
                .Where(e => e.Date >= start && e.Date <= end)
                .ToList();
            return (incomes, expenses);
        }

        public decimal CalculateTotalIncome(DateTime? start = null, DateTime? end = null)
        {
            IEnumerable<Income> items = Incomes;
            if (start.HasValue && end.HasValue)
                items = items.Where(i => i.Date >= start && i.Date <= end);
            return items.Sum(i => i.Amount);
        }

        public decimal CalculateTotalExpense(DateTime? start = null, DateTime? end = null)
        {
            IEnumerable<Expense> items = Expenses;
            if (start.HasValue && end.HasValue)
                items = items.Where(e => e.Date >= start && e.Date <= end);
            return items.Sum(e => e.Amount);
        }

        public decimal CalculateNetProfit(DateTime? start = null, DateTime? end = null)
        {
            return CalculateTotalIncome(start, end) - CalculateTotalExpense(start, end);
        }

        public Report GenerateReport(DateTime start, DateTime end)
        {
            var (incomes, expenses) = GetRecordsByPeriod(start, end);

            decimal totalIncome = 0m;
            foreach (var income in incomes) totalIncome += income.Amount;

            decimal totalExpense = 0m;
            foreach (var expense in expenses) totalExpense += expense.Amount;

            return new Report
            {
                StartDate = start,
                EndDate = end,
                TotalIncome = totalIncome,
                TotalExpense = totalExpense,
                Incomes = incomes,
                Expenses = expenses
            };
        }

        public void AddProfile(Profile profile)
        {
            Profiles.Add(profile);
            if (string.IsNullOrEmpty(ActiveProfileId))
                ActiveProfileId = profile.Id;
        }

        public bool SwitchProfile(string id)
        {
            foreach (var profile in Profiles)
            {
                if (profile.Id == id)
                {
                    ActiveProfileId = id;
                    return true;
                }
            }

            return false;
        }

        public void SetProfiles(List<Profile> profiles, string activeProfileId)
        {
            Profiles = profiles ?? new List<Profile>();
            ActiveProfileId = ResolveActiveProfileId(Profiles, activeProfileId);
        }

        public void LoadData(List<Income> incomes, List<Expense> expenses, List<Category> categories, Entrepreneur entrepreneur)
        {
            Incomes = incomes ?? new List<Income>();
            Expenses = expenses ?? new List<Expense>();
            Categories = categories ?? new List<Category>();
            Entrepreneur = entrepreneur ?? new Entrepreneur();
        }

        public void LoadData(List<Income> incomes, List<Expense> expenses, List<Category> categories)
        {
            LoadData(incomes, expenses, categories, Entrepreneur ?? new Entrepreneur());
        }

        private static string ResolveActiveProfileId(List<Profile> profiles, string activeProfileId)
        {
            if (profiles == null || profiles.Count == 0) return activeProfileId ?? string.Empty;

            foreach (var profile in profiles)
                if (profile.Id == activeProfileId) return activeProfileId;

            return profiles[0].Id;
        }
    }
}
