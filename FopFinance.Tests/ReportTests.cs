// ════════════════════════════════════════════════════════════════════════
//  FopFinance — Модульні тести: Report (бізнес-логіка + експорт)
//
//  Чек-лист №1 — Report (бізнес-логіка)
//  ──────────────────────────────────────────────────────────────────────
//  RP-01  NetProfit — обчислюється як різниця TotalIncome - TotalExpense
//  RP-02  NetProfit — від'ємне значення при збитку
//  RP-03  Generate() — текстовий підсумок містить дати, суми
//  RP-04  ExportToJSON() — результат є валідним JSON і містить ключові поля
//  RP-05  ExportToXML() — результат є валідним XML і містить суми
//  RP-06  ExportToCSV() — результат містить заголовок і рядки
//  RP-07  ExportToCSV() — поле з комою екранується
//  RP-08  ExportToJSON() — порожній звіт серіалізується без помилок
//  RP-09  ExportToXML() — вкладені Income та Expense елементи присутні
// ════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Xml.Linq;
using FopFinance.Models;
using Xunit;

namespace FopFinance.Tests;

/// <summary>
/// Тест-кейс RP-A: Властивості та текстовий вивід звіту.
/// </summary>
public class ReportPropertiesTests
{
    private static Report BuildReport(decimal income = 1000m, decimal expense = 300m) =>
        new()
        {
            StartDate    = new DateTime(2026, 1, 1),
            EndDate      = new DateTime(2026, 1, 31),
            TotalIncome  = income,
            TotalExpense = expense,
            Incomes      = new List<Income>(),
            Expenses     = new List<Expense>()
        };

    // ── RP-01 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "RP-01: NetProfit = TotalIncome - TotalExpense (позитивний)")]
    public void NetProfit_ReturnsCorrectPositiveValue()
    {
        var report = BuildReport(1000m, 300m);
        Assert.Equal(700m, report.NetProfit);
    }

    // ── RP-02 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "RP-02: NetProfit — від'ємне значення при збитку")]
    public void NetProfit_ReturnsNegativeWhenLoss()
    {
        var report = BuildReport(100m, 500m);
        Assert.Equal(-400m, report.NetProfit);
    }

    // ── RP-03 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "RP-03: Generate() — текстовий підсумок містить очікувані дані")]
    public void Generate_ContainsKeyInformation()
    {
        var report = BuildReport(1000m, 300m);
        var text = report.Generate();
        Assert.Contains("01.01.2026", text);
        Assert.Contains("31.01.2026", text);
    }
}

/// <summary>
/// Тест-кейс RP-B: Серіалізація звіту у різні формати.
/// </summary>
public class ReportExportTests
{
    private static Report BuildDetailedReport()
    {
        var catId = Guid.NewGuid().ToString();
        return new Report
        {
            StartDate    = new DateTime(2026, 2, 1),
            EndDate      = new DateTime(2026, 2, 28),
            TotalIncome  = 5000m,
            TotalExpense = 1200m,
            Incomes = new List<Income>
            {
                new()
                {
                    Id          = "inc-1",
                    Date        = new DateTime(2026, 2, 5),
                    Amount      = 5000m,
                    Source      = "ТОВ Клієнт",
                    Description = "Консалтинг"
                }
            },
            Expenses = new List<Expense>
            {
                new()
                {
                    Id           = "exp-1",
                    Date         = new DateTime(2026, 2, 10),
                    Amount       = 1200m,
                    CategoryId   = catId,
                    CategoryName = "Оренда",
                    Description  = "Офіс"
                }
            }
        };
    }

    // ── RP-04 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "RP-04: ExportToJSON() — валідний JSON із ключовими полями")]
    public void ExportToJSON_ProducesValidJsonWithKeyFields()
    {
        var report = BuildDetailedReport();
        var json = report.ExportToJSON();

        // Валідний JSON — не кидає виняток
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(5000m, root.GetProperty("TotalIncome") .GetDecimal());
        Assert.Equal(1200m, root.GetProperty("TotalExpense").GetDecimal());
        Assert.Equal(3800m, root.GetProperty("NetProfit")   .GetDecimal());
    }

    // ── RP-05 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "RP-05: ExportToXML() — валідний XML із коректними сумами")]
    public void ExportToXML_ProducesValidXmlWithAmounts()
    {
        var report = BuildDetailedReport();
        var xml = report.ExportToXML();

        var doc  = XDocument.Parse(xml);
        var root = doc.Root!;

        Assert.Equal("5000", root.Element("TotalIncome") ?.Value);
        Assert.Equal("1200", root.Element("TotalExpense")?.Value);
        Assert.Equal("3800", root.Element("NetProfit")   ?.Value);
    }

    // ── RP-06 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "RP-06: ExportToCSV() — містить заголовок та рядки доходів/витрат")]
    public void ExportToCSV_ContainsHeaderAndRows()
    {
        var report = BuildDetailedReport();
        var csv = report.ExportToCSV();

        Assert.Contains("Section,Date,Type,CategoryOrSource,Amount,Description", csv);
        Assert.Contains("ТОВ Клієнт", csv);
        Assert.Contains("Оренда",     csv);
        Assert.Contains("5000",       csv);
        Assert.Contains("1200",       csv);
    }

    // ── RP-07 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "RP-07: ExportToCSV() — поле з комою береться в лапки")]
    public void ExportToCSV_FieldWithComma_IsQuoted()
    {
        var report = new Report
        {
            StartDate    = new DateTime(2026, 1, 1),
            EndDate      = new DateTime(2026, 1, 31),
            TotalIncome  = 100m,
            TotalExpense = 0m,
            Incomes = new List<Income>
            {
                new()
                {
                    Date   = new DateTime(2026, 1, 5),
                    Amount = 100m,
                    Source = "Client, Ltd"   // містить кому
                }
            },
            Expenses = new List<Expense>()
        };

        var csv = report.ExportToCSV();
        Assert.Contains("\"Client, Ltd\"", csv);
    }

    // ── RP-08 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "RP-08: ExportToJSON() — порожній звіт серіалізується без виняток")]
    public void ExportToJSON_EmptyReport_NoException()
    {
        var report = new Report
        {
            StartDate    = DateTime.Today,
            EndDate      = DateTime.Today,
            TotalIncome  = 0m,
            TotalExpense = 0m
        };

        var ex = Record.Exception(() => report.ExportToJSON());
        Assert.Null(ex);
    }

    // ── RP-09 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "RP-09: ExportToXML() — Income та Expense елементи присутні в XML")]
    public void ExportToXML_ContainsIncomeAndExpenseElements()
    {
        var report = BuildDetailedReport();
        var xml = report.ExportToXML();
        var doc  = XDocument.Parse(xml);
        var root = doc.Root!;

        var incomesEl  = root.Element("Incomes");
        var expensesEl = root.Element("Expenses");

        Assert.NotNull(incomesEl);
        Assert.NotNull(expensesEl);
        Assert.Single(incomesEl!.Elements("Income"));
        Assert.Single(expensesEl!.Elements("Expense"));
        Assert.Equal("ТОВ Клієнт", incomesEl.Element("Income")?.Element("Source")?.Value);
        Assert.Equal("Оренда",     expensesEl.Element("Expense")?.Element("Category")?.Value);
    }
}
