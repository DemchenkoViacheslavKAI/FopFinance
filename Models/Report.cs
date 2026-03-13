using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Text.Json;

namespace FopFinance.Models
{
    /// <summary>
    /// Клас «Звіт» — підсумок фінансових операцій за заданий період.
    /// Підтримує експорт у JSON та XML.
    /// </summary>
    public class Report
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal TotalExpense { get; set; }
        public decimal NetProfit => TotalIncome - TotalExpense;

        /// <summary>Список доходів у звітному періоді.</summary>
        public List<Income> Incomes { get; set; } = new();

        /// <summary>Список витрат у звітному періоді.</summary>
        public List<Expense> Expenses { get; set; } = new();

        /// <summary>
        /// Генерує текстовий підсумок звіту для відображення у UI.
        /// </summary>
        public string Generate()
        {
            return $"Звіт за {StartDate:dd.MM.yyyy} – {EndDate:dd.MM.yyyy}\n" +
                   $"Доходи: {TotalIncome:C}\n" +
                   $"Витрати: {TotalExpense:C}\n" +
                   $"Чистий прибуток: {NetProfit:C}";
        }

        /// <summary>Серіалізує звіт у рядок формату JSON.</summary>
        public string ExportToJSON()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            return JsonSerializer.Serialize(this, options);
        }

        /// <summary>Серіалізує звіт у рядок формату XML.</summary>
        public string ExportToXML()
        {
            var root = new XElement("Report",
                new XElement("Period",
                    new XElement("StartDate", StartDate.ToString("yyyy-MM-dd")),
                    new XElement("EndDate", EndDate.ToString("yyyy-MM-dd"))),
                new XElement("TotalIncome", TotalIncome),
                new XElement("TotalExpense", TotalExpense),
                new XElement("NetProfit", NetProfit),
                new XElement("Incomes", BuildIncomeElements()),
                new XElement("Expenses", BuildExpenseElements()));

            return new XDocument(new XDeclaration("1.0", "utf-8", null), root)
                .ToString();
        }

        // --- Допоміжні методи для XML ---

        private List<XElement> BuildIncomeElements()
        {
            var list = new List<XElement>();
            foreach (var i in Incomes)
                list.Add(new XElement("Income",
                    new XElement("Id", i.Id),
                    new XElement("Date", i.Date.ToString("yyyy-MM-dd")),
                    new XElement("Amount", i.Amount),
                    new XElement("Source", i.Source),
                    new XElement("Description", i.Description)));
            return list;
        }

        private List<XElement> BuildExpenseElements()
        {
            var list = new List<XElement>();
            foreach (var e in Expenses)
                list.Add(new XElement("Expense",
                    new XElement("Id", e.Id),
                    new XElement("Date", e.Date.ToString("yyyy-MM-dd")),
                    new XElement("Amount", e.Amount),
                    new XElement("Category", e.CategoryName),
                    new XElement("Description", e.Description)));
            return list;
        }
    }
}
