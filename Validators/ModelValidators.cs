using System;
using FopFinance.Models;

namespace FopFinance.Validators
{
    public static class FinancialRecordValidator
    {
        public static string Validate(FinancialRecord record)
        {
            if (record.Amount <= 0)
                return "Сума повинна бути більшою за нуль.";

            if (record.Date == default)
                return "Дата не може бути порожньою.";

            if (record.Date > DateTime.Today.AddDays(1))
                return "Дата не може бути в майбутньому.";

            return string.Empty;
        }
    }

    public static class IncomeValidator
    {
        public static string Validate(Income income)
        {
            string baseError = FinancialRecordValidator.Validate(income);
            if (!string.IsNullOrEmpty(baseError)) return baseError;

            if (string.IsNullOrWhiteSpace(income.Source))
                return "Джерело доходу є обов'язковим полем.";

            return string.Empty;
        }
    }

    public static class ExpenseValidator
    {
        public static string Validate(Expense expense)
        {
            string baseError = FinancialRecordValidator.Validate(expense);
            if (!string.IsNullOrEmpty(baseError)) return baseError;

            if (string.IsNullOrWhiteSpace(expense.CategoryId))
                return "Витрата повинна мати категорію.";

            return string.Empty;
        }
    }
}
