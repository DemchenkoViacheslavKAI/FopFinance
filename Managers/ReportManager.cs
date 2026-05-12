using System;
using FopFinance.Models;

namespace FopFinance.Managers
{
    /// <summary>
    /// Generates financial reports from income and expense managers.
    /// </summary>
    public class ReportManager
    {
        private readonly IncomeManager _incomes;
        private readonly ExpenseManager _expenses;

        public ReportManager(IncomeManager incomes, ExpenseManager expenses)
        {
            _incomes = incomes;
            _expenses = expenses;
        }

        public Report Generate(DateTime start, DateTime end)
        {
            var incomes = _incomes.GetByPeriod(start, end);
            var expenses = _expenses.GetByPeriod(start, end);

            decimal totalIncome = 0;
            foreach (var i in incomes) totalIncome += i.Amount;

            decimal totalExpense = 0;
            foreach (var e in expenses) totalExpense += e.Amount;

            return new Report
            {
                StartDate    = start,
                EndDate      = end,
                TotalIncome  = totalIncome,
                TotalExpense = totalExpense,
                Incomes      = incomes,
                Expenses     = expenses
            };
        }
    }
}
