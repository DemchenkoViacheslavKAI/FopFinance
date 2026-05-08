// ════════════════════════════════════════════════════════════════════════
//  FopFinance — Модульні тести: Моделі (бізнес-логіка)
//
//  Чек-лист №1 — Бізнес-логіка (FinancialRecord, Income, Expense, Category)
//  ──────────────────────────────────────────────────────────────────────
//  BL-01  FinancialRecord.Validate() — сума = 0 → помилка
//  BL-02  FinancialRecord.Validate() — від'ємна сума → помилка
//  BL-03  FinancialRecord.Validate() — дата за замовчуванням → помилка
//  BL-04  FinancialRecord.Validate() — майбутня дата → помилка
//  BL-05  FinancialRecord.Validate() — коректні дані → порожній рядок
//  BL-06  FinancialRecord.GetAmount() — повертає Amount
//  BL-07  Income.Validate() — порожнє Source → помилка
//  BL-08  Income.Validate() — пробільне Source → помилка
//  BL-09  Income.Validate() — коректні дані → порожній рядок
//  BL-10  Income.CalculateTax() — 5% за замовчуванням
//  BL-11  Income.CalculateTax() — 3% для 1-ї групи
//  BL-12  Income.CalculateTax() — 0% → нуль
//  BL-13  Income.CalculateTax() — округлення до 2 знаків
//  BL-14  Expense.Validate() — порожній CategoryId → помилка
//  BL-15  Expense.Validate() — коректні дані → порожній рядок
//  BL-16  Expense.AssignCategory() — встановлює Id та Name
//  BL-17  Expense.AssignCategory() — null → ArgumentNullException
//  BL-18  Category.Edit() — оновлює Name та Description
//  BL-19  Category — унікальний Id при кожному new()
//  BL-20  Entrepreneur.GetFinancialSummary() — рядок містить усі дані
// ════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using FopFinance.Models;
using Xunit;

namespace FopFinance.Tests;

/// <summary>
/// Тест-кейс BL-A: Валідація базового запису (FinancialRecord).
/// Перевіряє всі гілки методу Validate() — позитивні та негативні сценарії.
/// </summary>
public class FinancialRecordValidationTests
{
    // ── Допоміжна конкретна реалізація (Income достатньо) ──────────────

    private static Income ValidIncome(decimal amount = 1000m) =>
        new() { Date = DateTime.Today, Amount = amount, Source = "Client" };

    // ── BL-01: Нульова сума ───────────────────────────────────────────
    [Fact(DisplayName = "BL-01: Amount=0 → Validate повертає помилку")]
    public void Validate_ZeroAmount_ReturnsError()
    {
        var income = new Income { Date = DateTime.Today, Amount = 0, Source = "X" };
        var result = income.Validate();
        Assert.Equal("Сума повинна бути більшою за нуль.", result);
    }

    // ── BL-02: Від'ємна сума ─────────────────────────────────────────
    [Fact(DisplayName = "BL-02: Від'ємна сума → Validate повертає помилку")]
    public void Validate_NegativeAmount_ReturnsError()
    {
        var income = new Income { Date = DateTime.Today, Amount = -100m, Source = "X" };
        var result = income.Validate();
        Assert.Equal("Сума повинна бути більшою за нуль.", result);
    }

    // ── BL-03: Дата = default ────────────────────────────────────────
    [Fact(DisplayName = "BL-03: Дата за замовчуванням → Validate повертає помилку")]
    public void Validate_DefaultDate_ReturnsError()
    {
        var income = new Income { Date = default, Amount = 500m, Source = "X" };
        var result = income.Validate();
        Assert.Equal("Дата не може бути порожньою.", result);
    }

    // ── BL-04: Майбутня дата ─────────────────────────────────────────
    [Fact(DisplayName = "BL-04: Дата у майбутньому → Validate повертає помилку")]
    public void Validate_FutureDate_ReturnsError()
    {
        var income = new Income
        {
            Date   = DateTime.Today.AddDays(10),
            Amount = 500m,
            Source = "X"
        };
        var result = income.Validate();
        Assert.Equal("Дата не може бути в майбутньому.", result);
    }

    // ── BL-05: Коректні дані → успіх ────────────────────────────────
    [Fact(DisplayName = "BL-05: Коректні дані → Validate повертає порожній рядок")]
    public void Validate_ValidData_ReturnsEmpty()
    {
        var income = ValidIncome();
        var result = income.Validate();
        Assert.Equal(string.Empty, result);
    }

    // ── BL-06: GetAmount() ───────────────────────────────────────────
    [Fact(DisplayName = "BL-06: GetAmount() повертає значення Amount")]
    public void GetAmount_ReturnsAmountValue()
    {
        var income = ValidIncome(777.50m);
        Assert.Equal(777.50m, income.GetAmount());
    }

    // ── BL-04b: Дата = UtcNow+2 все одно відхиляється ───────────────
    [Fact(DisplayName = "BL-04b: Дата = завтра+1 → Validate повертає помилку")]
    public void Validate_DateMoreThanOneDayAhead_ReturnsError()
    {
        var income = new Income
        {
            Date   = DateTime.Today.AddDays(2),
            Amount = 1m,
            Source = "X"
        };
        Assert.NotEmpty(income.Validate());
    }
}

/// <summary>
/// Тест-кейс BL-B: Бізнес-правила Income.
/// Перевіряє додаткову валідацію та розрахунок податку.
/// </summary>
public class IncomeBusinessRulesTests
{
    // ── BL-07: Порожнє джерело ───────────────────────────────────────
    [Fact(DisplayName = "BL-07: Порожнє Source → Validate повертає помилку")]
    public void Validate_EmptySource_ReturnsError()
    {
        var income = new Income { Date = DateTime.Today, Amount = 100m, Source = "" };
        var result = income.Validate();
        Assert.Equal("Джерело доходу є обов'язковим полем.", result);
    }

    // ── BL-08: Пробільне джерело ─────────────────────────────────────
    [Fact(DisplayName = "BL-08: Пробільне Source → Validate повертає помилку")]
    public void Validate_WhitespaceSource_ReturnsError()
    {
        var income = new Income { Date = DateTime.Today, Amount = 100m, Source = "   " };
        var result = income.Validate();
        Assert.Equal("Джерело доходу є обов'язковим полем.", result);
    }

    // ── BL-09: Коректний Income ──────────────────────────────────────
    [Fact(DisplayName = "BL-09: Income з усіма полями → Validate повертає порожній рядок")]
    public void Validate_ValidIncome_ReturnsEmpty()
    {
        var income = new Income { Date = DateTime.Today, Amount = 1000m, Source = "АТ Ромашка" };
        Assert.Equal(string.Empty, income.Validate());
    }

    // ── BL-10: Податок 5% за замовчуванням ───────────────────────────
    [Fact(DisplayName = "BL-10: CalculateTax() без аргументу → 5% від суми")]
    public void CalculateTax_Default5Percent()
    {
        var income = new Income { Amount = 10_000m };
        Assert.Equal(500m, income.CalculateTax());
    }

    // ── BL-11: Податок 3% (1-а група) ────────────────────────────────
    [Fact(DisplayName = "BL-11: CalculateTax(3) → 3% від суми")]
    public void CalculateTax_ThreePercent()
    {
        var income = new Income { Amount = 10_000m };
        Assert.Equal(300m, income.CalculateTax(3m));
    }

    // ── BL-12: Податок 0% → нуль ────────────────────────────────────
    [Fact(DisplayName = "BL-12: CalculateTax(0) → 0")]
    public void CalculateTax_ZeroRate_ReturnsZero()
    {
        var income = new Income { Amount = 9999m };
        Assert.Equal(0m, income.CalculateTax(0m));
    }

    // ── BL-13: Округлення до 2 знаків ────────────────────────────────
    [Fact(DisplayName = "BL-13: CalculateTax() округляє до 2 знаків")]
    public void CalculateTax_RoundsToTwoDecimals()
    {
        // 333.33 * 5% = 16.6665 → 16.67
        var income = new Income { Amount = 333.33m };
        var tax = income.CalculateTax(5m);
        Assert.Equal(16.67m, tax);
    }

    // ── Параметризований: різні ставки ───────────────────────────────
    public static IEnumerable<object[]> CalculateTaxCases =>
        new[]
        {
            new object[] { 1000m, 5m, 50.00m },
            new object[] { 2000m, 3m, 60.00m },
            new object[] { 500m, 20m, 100.00m },
            new object[] { 1m, 5m, 0.05m }
        };

    [Theory(DisplayName = "CalculateTax — параметризований розрахунок")]
    [MemberData(nameof(CalculateTaxCases))]
    public void CalculateTax_Theory(decimal amount, decimal rate, decimal expected)
    {
        var income = new Income { Amount = amount };
        Assert.Equal(expected, income.CalculateTax(rate));
    }
}

/// <summary>
/// Тест-кейс BL-C: Бізнес-правила Expense та Category.
/// </summary>
public class ExpenseAndCategoryTests
{
    // ── BL-14: Порожній CategoryId ───────────────────────────────────
    [Fact(DisplayName = "BL-14: Порожній CategoryId → Validate повертає помилку")]
    public void Expense_Validate_EmptyCategoryId_ReturnsError()
    {
        var expense = new Expense
        {
            Date       = DateTime.Today,
            Amount     = 100m,
            CategoryId = ""
        };
        var result = expense.Validate();
        Assert.Equal("Витрата повинна мати категорію.", result);
    }

    // ── BL-15: Коректна витрата ──────────────────────────────────────
    [Fact(DisplayName = "BL-15: Витрата з CategoryId → Validate повертає порожній рядок")]
    public void Expense_Validate_ValidData_ReturnsEmpty()
    {
        var expense = new Expense
        {
            Date       = DateTime.Today,
            Amount     = 500m,
            CategoryId = Guid.NewGuid().ToString()
        };
        Assert.Equal(string.Empty, expense.Validate());
    }

    // ── BL-16: AssignCategory встановлює Id та Name ───────────────────
    [Fact(DisplayName = "BL-16: AssignCategory() встановлює CategoryId та CategoryName")]
    public void Expense_AssignCategory_SetsBothFields()
    {
        var cat = new Category { Name = "Транспорт" };
        var expense = new Expense();
        expense.AssignCategory(cat);
        Assert.Equal(cat.Id,   expense.CategoryId);
        Assert.Equal("Транспорт", expense.CategoryName);
    }

    // ── BL-17: AssignCategory(null) → виняток ─────────────────────────
    [Fact(DisplayName = "BL-17: AssignCategory(null) → ArgumentNullException")]
    public void Expense_AssignCategory_Null_ThrowsArgumentNullException()
    {
        var expense = new Expense();
        Assert.Throws<ArgumentNullException>(() => expense.AssignCategory(null!));
    }

    // ── BL-18: Category.Edit() ────────────────────────────────────────
    [Fact(DisplayName = "BL-18: Category.Edit() оновлює Name та Description")]
    public void Category_Edit_UpdatesNameAndDescription()
    {
        var cat = new Category { Name = "Старе", Description = "" };
        cat.Edit("Нове", "Нові витрати");
        Assert.Equal("Нове", cat.Name);
        Assert.Equal("Нові витрати", cat.Description);
    }

    // ── BL-19: Унікальний Id ──────────────────────────────────────────
    [Fact(DisplayName = "BL-19: Кожна нова Category отримує унікальний Id")]
    public void Category_NewInstance_HasUniqueId()
    {
        var ids = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < 100; i++)
            ids.Add(new Category().Id);
        Assert.Equal(100, ids.Count);
    }

    // ── BL-20: Entrepreneur.GetFinancialSummary() ─────────────────────
    [Fact(DisplayName = "BL-20: GetFinancialSummary() містить ПІБ, групу та РНОКПП")]
    public void Entrepreneur_GetFinancialSummary_ContainsAllData()
    {
        var e = new Entrepreneur
        {
            FullName           = "Коваль Іван",
            TaxGroup           = 2,
            RegistrationNumber = "9876543210"
        };
        var summary = e.GetFinancialSummary();
        Assert.Contains("Коваль Іван",  summary);
        Assert.Contains("2",            summary);
        Assert.Contains("9876543210",   summary);
    }
}
