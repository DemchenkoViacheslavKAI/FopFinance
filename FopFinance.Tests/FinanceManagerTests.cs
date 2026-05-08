// ════════════════════════════════════════════════════════════════════════
//  FopFinance — Модульні тести: FinanceManager (бізнес-логіка)
//
//  Чек-лист №1 — FinanceManager (продовження)
//  ──────────────────────────────────────────────────────────────────────
//  FM-01  AddIncome — валідний дохід додається до списку
//  FM-02  AddIncome — дублікат Id не призводить до помилки (обидва додаються)
//  FM-03  AddIncome — невалідний дохід (нульова сума) не додається
//  FM-04  UpdateIncome — оновлює існуючий запис
//  FM-05  UpdateIncome — неіснуючий Id → помилка
//  FM-06  UpdateIncome — невалідні дані → помилка, запис не змінюється
//  FM-07  AddExpense — встановлює CategoryName з каталогу
//  FM-08  AddExpense — невідома категорія → помилка
//  FM-09  AddExpense — невалідна витрата → помилка
//  FM-10  UpdateExpense — оновлює запис та CategoryName
//  FM-11  UpdateExpense — невалідна витрата → помилка
//  FM-12  UpdateExpense — неіснуючий Id → помилка
//  FM-13  RemoveRecord — видаляє дохід за Id
//  FM-14  RemoveRecord — видаляє витрату за Id
//  FM-15  RemoveRecord — неіснуючий Id → false
//  FM-16  AddCategory — успішне додавання
//  FM-17  AddCategory — дублікат назви → помилка (case-insensitive)
//  FM-18  AddCategory — порожня назва → помилка
//  FM-19  AddCategory — назва з пробілів → помилка
//  FM-20  UpdateCategory — оновлює назву
//  FM-21  UpdateCategory — порожня назва → помилка
//  FM-22  UpdateCategory — неіснуючий Id → помилка
//  FM-23  RemoveCategory — успішне видалення (без витрат)
//  FM-24  RemoveCategory — категорія із витратами → помилка
//  FM-25  RemoveCategory — неіснуючий Id → помилка
//  FM-26  GetRecordsByPeriod — повертає лише записи у межах діапазону
//  FM-27  GetRecordsByPeriod — межі включно (start=end=date)
//  FM-28  CalculateTotalIncome — без фільтра → сума всіх доходів
//  FM-29  CalculateTotalIncome — з фільтром → лише за期間
//  FM-30  CalculateTotalExpense — без фільтра → сума всіх витрат
//  FM-31  CalculateNetProfit — позитивний прибуток
//  FM-32  CalculateNetProfit — збиток (від'ємне значення)
//  FM-33  GenerateReport — правильні суми та склади
//  FM-34  GenerateReport — порожній період → нулі
//  FM-35  AddProfile — додає профіль і встановлює ActiveProfileId
//  FM-36  SwitchProfile — перемикає активний профіль
//  FM-37  SwitchProfile — неіснуючий профіль → false, Id не змінюється
//  FM-38  SetProfiles — ініціалізує колекцію та ActiveProfileId
//  FM-39  LoadData — замінює всі колекції
//  FM-40  LoadData — null-списки замінюються порожніми колекціями
// ════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using FopFinance.Managers;
using FopFinance.Models;
using Xunit;

namespace FopFinance.Tests;

/// <summary>
/// Тест-кейс FM-A: Операції з доходами (CRUD).
/// Ізоляція: FinanceManager без жодних зовнішніх залежностей.
/// </summary>
public class FinanceManager_IncomeTests
{
    // Фабрика: чистий менеджер для кожного тесту (ізоляція)
    private static FinanceManager Fresh() => new();

    private static Income ValidIncome(string source = "Client", decimal amount = 1000m) =>
        new() { Date = DateTime.Today, Amount = amount, Source = source };

    // ── FM-01 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-01: AddIncome з коректними даними → дохід у списку")]
    public void AddIncome_ValidIncome_AddsToList()
    {
        var mgr = Fresh();
        var err = mgr.AddIncome(ValidIncome());

        Assert.Equal(string.Empty, err);
        Assert.Single(mgr.Incomes);
    }

    // ── FM-02 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-02: Два різних доходи → обидва у списку")]
    public void AddIncome_TwoIncomes_BothInList()
    {
        var mgr = Fresh();
        mgr.AddIncome(ValidIncome("A", 100m));
        mgr.AddIncome(ValidIncome("B", 200m));

        Assert.Equal(2, mgr.Incomes.Count);
    }

    // ── FM-03 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-03: AddIncome з нульовою сумою → помилка, список порожній")]
    public void AddIncome_ZeroAmount_NotAdded()
    {
        var mgr = Fresh();
        var income = new Income { Date = DateTime.Today, Amount = 0m, Source = "X" };
        var err = mgr.AddIncome(income);

        Assert.NotEmpty(err);
        Assert.Empty(mgr.Incomes);
    }

    // ── FM-04 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-04: UpdateIncome → дохід оновлено")]
    public void UpdateIncome_ExistingId_UpdatesRecord()
    {
        var mgr = Fresh();
        var income = ValidIncome("OldClient");
        mgr.AddIncome(income);

        income.Source = "NewClient";
        income.Amount = 2000m;
        var err = mgr.UpdateIncome(income);

        Assert.Equal(string.Empty, err);
        Assert.Equal("NewClient", mgr.Incomes[0].Source);
        Assert.Equal(2000m,       mgr.Incomes[0].Amount);
    }

    // ── FM-05 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-05: UpdateIncome з неіснуючим Id → помилка")]
    public void UpdateIncome_UnknownId_ReturnsError()
    {
        var mgr = Fresh();
        var income = ValidIncome();
        income.Id = "does-not-exist";

        var err = mgr.UpdateIncome(income);
        Assert.Equal("Запис не знайдено.", err);
    }

    // ── FM-06 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-06: UpdateIncome з невалідними даними → помилка, дані незмінні")]
    public void UpdateIncome_InvalidData_RecordNotChanged()
    {
        var mgr = Fresh();
        var income = ValidIncome("Stable", 500m);
        mgr.AddIncome(income);

        var broken = new Income { Id = income.Id, Date = DateTime.Today, Amount = -1m, Source = "X" };
        var err = mgr.UpdateIncome(broken);

        Assert.NotEmpty(err);
        Assert.Equal(500m, mgr.Incomes[0].Amount);
    }
}

/// <summary>
/// Тест-кейс FM-B: Операції з витратами (CRUD).
/// </summary>
public class FinanceManager_ExpenseTests
{
    private static (FinanceManager mgr, Category cat) FreshWithCategory()
    {
        var mgr = new FinanceManager();
        var cat = new Category { Name = "Оренда" };
        mgr.AddCategory(cat);
        return (mgr, cat);
    }

    private static Expense ValidExpense(string categoryId, decimal amount = 500m) =>
        new() { Date = DateTime.Today, Amount = amount, CategoryId = categoryId };

    // ── FM-07 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-07: AddExpense → CategoryName береться з каталогу категорій")]
    public void AddExpense_SetsCategoryNameFromCatalog()
    {
        var (mgr, cat) = FreshWithCategory();
        var expense = new Expense
        {
            Date       = DateTime.Today,
            Amount     = 300m,
            CategoryId = cat.Id,
            CategoryName = "WRONG"   // має бути перезаписано
        };

        var err = mgr.AddExpense(expense);

        Assert.Equal(string.Empty, err);
        Assert.Equal("Оренда", mgr.Expenses[0].CategoryName);
    }

    // ── FM-08 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-08: AddExpense з невідомою категорією → помилка")]
    public void AddExpense_UnknownCategory_ReturnsError()
    {
        var mgr = new FinanceManager();
        var expense = ValidExpense("ghost-id");

        var err = mgr.AddExpense(expense);
        Assert.Equal("Вибрана категорія не існує.", err);
        Assert.Empty(mgr.Expenses);
    }

    // ── FM-09 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-09: AddExpense з нульовою сумою → помилка")]
    public void AddExpense_ZeroAmount_ReturnsError()
    {
        var (mgr, cat) = FreshWithCategory();
        var expense = new Expense
        {
            Date = DateTime.Today, Amount = 0m, CategoryId = cat.Id
        };
        var err = mgr.AddExpense(expense);
        Assert.NotEmpty(err);
    }

    // ── FM-10 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-10: UpdateExpense → запис оновлено, CategoryName синхронізовано")]
    public void UpdateExpense_ValidData_UpdatesRecord()
    {
        var (mgr, cat) = FreshWithCategory();
        var expense = ValidExpense(cat.Id, 200m);
        mgr.AddExpense(expense);

        expense.Amount = 999m;
        var err = mgr.UpdateExpense(expense);

        Assert.Equal(string.Empty, err);
        Assert.Equal(999m, mgr.Expenses[0].Amount);
        Assert.Equal("Оренда", mgr.Expenses[0].CategoryName);
    }

    // ── FM-11 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-11: UpdateExpense з невалідними даними → помилка")]
    public void UpdateExpense_InvalidData_ReturnsError()
    {
        var (mgr, cat) = FreshWithCategory();
        var expense = ValidExpense(cat.Id);
        mgr.AddExpense(expense);

        expense.Amount = 0;
        var err = mgr.UpdateExpense(expense);
        Assert.NotEmpty(err);
    }

    // ── FM-12 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-12: UpdateExpense з неіснуючим Id → помилка")]
    public void UpdateExpense_UnknownId_ReturnsError()
    {
        var (mgr, cat) = FreshWithCategory();
        var expense = ValidExpense(cat.Id);
        expense.Id = "ghost";
        var err = mgr.UpdateExpense(expense);
        Assert.Equal("Запис не знайдено.", err);
    }
}

/// <summary>
/// Тест-кейс FM-C: Видалення записів.
/// </summary>
public class FinanceManager_RemoveTests
{
    // ── FM-13 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-13: RemoveRecord — видаляє дохід за Id")]
    public void RemoveRecord_IncomeId_RemovesIncome()
    {
        var mgr = new FinanceManager();
        var income = new Income { Date = DateTime.Today, Amount = 100m, Source = "X" };
        mgr.AddIncome(income);

        var result = mgr.RemoveRecord(income.Id);

        Assert.True(result);
        Assert.Empty(mgr.Incomes);
    }

    // ── FM-14 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-14: RemoveRecord — видаляє витрату за Id")]
    public void RemoveRecord_ExpenseId_RemovesExpense()
    {
        var mgr = new FinanceManager();
        var cat = new Category { Name = "Транспорт" };
        mgr.AddCategory(cat);
        var expense = new Expense { Date = DateTime.Today, Amount = 200m, CategoryId = cat.Id };
        mgr.AddExpense(expense);

        var result = mgr.RemoveRecord(expense.Id);

        Assert.True(result);
        Assert.Empty(mgr.Expenses);
    }

    // ── FM-15 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-15: RemoveRecord — неіснуючий Id → false")]
    public void RemoveRecord_UnknownId_ReturnsFalse()
    {
        var mgr = new FinanceManager();
        var result = mgr.RemoveRecord("ghost-id");
        Assert.False(result);
    }
}

/// <summary>
/// Тест-кейс FM-D: Управління категоріями.
/// </summary>
public class FinanceManager_CategoryTests
{
    // ── FM-16 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-16: AddCategory — нова категорія додається")]
    public void AddCategory_ValidName_AddsToList()
    {
        var mgr = new FinanceManager();
        var err = mgr.AddCategory(new Category { Name = "Реклама" });

        Assert.Equal(string.Empty, err);
        Assert.Single(mgr.Categories);
    }

    // ── FM-17 ────────────────────────────────────────────────────────
    [Theory(DisplayName = "FM-17: AddCategory — дублікат назви (case-insensitive) → помилка")]
    [InlineData("Реклама",  "реклама")]
    [InlineData("Реклама",  "РЕКЛАМА")]
    [InlineData("Реклама",  "Реклама")]
    public void AddCategory_DuplicateName_ReturnsError(string first, string second)
    {
        var mgr = new FinanceManager();
        mgr.AddCategory(new Category { Name = first });
        var err = mgr.AddCategory(new Category { Name = second });
        Assert.Equal("Категорія з такою назвою вже існує.", err);
    }

    // ── FM-18 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-18: AddCategory — порожня назва → помилка")]
    public void AddCategory_EmptyName_ReturnsError()
    {
        var mgr = new FinanceManager();
        var err = mgr.AddCategory(new Category { Name = "" });
        Assert.Equal("Назва категорії є обов'язковою.", err);
    }

    // ── FM-19 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-19: AddCategory — назва з пробілів → помилка")]
    public void AddCategory_WhitespaceName_ReturnsError()
    {
        var mgr = new FinanceManager();
        var err = mgr.AddCategory(new Category { Name = "   " });
        Assert.Equal("Назва категорії є обов'язковою.", err);
    }

    // ── FM-20 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-20: UpdateCategory — успішне оновлення назви")]
    public void UpdateCategory_ValidData_UpdatesName()
    {
        var mgr = new FinanceManager();
        var cat = new Category { Name = "Старе" };
        mgr.AddCategory(cat);

        cat.Name = "Нове";
        var err = mgr.UpdateCategory(cat);

        Assert.Equal(string.Empty, err);
        Assert.Equal("Нове", mgr.Categories[0].Name);
    }

    // ── FM-21 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-21: UpdateCategory — порожня назва → помилка")]
    public void UpdateCategory_EmptyName_ReturnsError()
    {
        var mgr = new FinanceManager();
        var cat = new Category { Name = "Valid" };
        mgr.AddCategory(cat);
        cat.Name = "";
        var err = mgr.UpdateCategory(cat);
        Assert.Equal("Назва категорії є обов'язковою.", err);
    }

    // ── FM-22 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-22: UpdateCategory — неіснуючий Id → помилка")]
    public void UpdateCategory_UnknownId_ReturnsError()
    {
        var mgr = new FinanceManager();
        var err = mgr.UpdateCategory(new Category { Id = "ghost", Name = "X" });
        Assert.Equal("Категорію не знайдено.", err);
    }

    // ── FM-23 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-23: RemoveCategory — без витрат → успішно")]
    public void RemoveCategory_NoExpenses_RemovesCategory()
    {
        var mgr = new FinanceManager();
        var cat = new Category { Name = "Логістика" };
        mgr.AddCategory(cat);

        var err = mgr.RemoveCategory(cat.Id);

        Assert.Equal(string.Empty, err);
        Assert.Empty(mgr.Categories);
    }

    // ── FM-24 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-24: RemoveCategory — з витратами → помилка")]
    public void RemoveCategory_WithExpenses_ReturnsError()
    {
        var mgr = new FinanceManager();
        var cat = new Category { Name = "Реклама" };
        mgr.AddCategory(cat);
        mgr.AddExpense(new Expense
        {
            Date = DateTime.Today, Amount = 100m, CategoryId = cat.Id
        });

        var err = mgr.RemoveCategory(cat.Id);
        Assert.Equal("Не можна видалити категорію, що використовується у витратах.", err);
    }

    // ── FM-25 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-25: RemoveCategory — неіснуючий Id → помилка")]
    public void RemoveCategory_UnknownId_ReturnsError()
    {
        var mgr = new FinanceManager();
        var err = mgr.RemoveCategory("ghost-id");
        Assert.Equal("Категорію не знайдено.", err);
    }
}

/// <summary>
/// Тест-кейс FM-E: Фільтрація та агрегації.
/// </summary>
public class FinanceManager_AggregationTests
{
    private static FinanceManager BuildSampleManager()
    {
        var mgr = new FinanceManager();
        var cat = new Category { Name = "Test" };
        mgr.AddCategory(cat);

        // Березень
        mgr.AddIncome (new Income  { Date = new DateTime(2026, 3, 1),  Amount = 1000m, Source = "A" });
        mgr.AddIncome (new Income  { Date = new DateTime(2026, 3, 15), Amount = 500m,  Source = "B" });
        mgr.AddExpense(new Expense { Date = new DateTime(2026, 3, 5),  Amount = 200m,  CategoryId = cat.Id });
        // Квітень
        mgr.AddIncome (new Income  { Date = new DateTime(2026, 4, 1),  Amount = 800m,  Source = "C" });
        mgr.AddExpense(new Expense { Date = new DateTime(2026, 4, 10), Amount = 350m,  CategoryId = cat.Id });

        return mgr;
    }

    // ── FM-26 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-26: GetRecordsByPeriod — повертає лише записи у межах діапазону")]
    public void GetRecordsByPeriod_FiltersByDate()
    {
        var mgr = BuildSampleManager();
        var (inc, exp) = mgr.GetRecordsByPeriod(
            new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

        Assert.Equal(2, inc.Count);
        Assert.Single(exp);
    }

    // ── FM-27 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-27: GetRecordsByPeriod — межі включно (start=end=date)")]
    public void GetRecordsByPeriod_InclusiveBounds()
    {
        var mgr = new FinanceManager();
        var targetDate = DateTime.Today;
        mgr.AddIncome(new Income { Date = targetDate, Amount = 100m, Source = "X" });

        var (inc, _) = mgr.GetRecordsByPeriod(targetDate, targetDate);

        Assert.Single(inc);
    }

    // ── FM-28 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-28: CalculateTotalIncome без фільтра → сума всіх доходів")]
    public void CalculateTotalIncome_NoFilter_SumsAll()
    {
        var mgr = BuildSampleManager();
        Assert.Equal(2300m, mgr.CalculateTotalIncome());
    }

    // ── FM-29 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-29: CalculateTotalIncome з фільтром → тільки за 期間")]
    public void CalculateTotalIncome_WithFilter_SumsOnlyFiltered()
    {
        var mgr = BuildSampleManager();
        var total = mgr.CalculateTotalIncome(new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));
        Assert.Equal(1500m, total);
    }

    // ── FM-30 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-30: CalculateTotalExpense без фільтра → сума всіх витрат")]
    public void CalculateTotalExpense_NoFilter_SumsAll()
    {
        var mgr = BuildSampleManager();
        Assert.Equal(550m, mgr.CalculateTotalExpense());
    }

    // ── FM-31 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-31: CalculateNetProfit → позитивний прибуток")]
    public void CalculateNetProfit_PositiveProfit()
    {
        var mgr = BuildSampleManager();
        // 2300 - 550 = 1750
        Assert.Equal(1750m, mgr.CalculateNetProfit());
    }

    // ── FM-32 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-32: CalculateNetProfit → збиток (від'ємне значення)")]
    public void CalculateNetProfit_Loss_ReturnsNegative()
    {
        var mgr = new FinanceManager();
        var cat = new Category { Name = "Big" };
        mgr.AddCategory(cat);
        mgr.AddIncome (new Income  { Date = DateTime.Today, Amount = 100m, Source = "X" });
        mgr.AddExpense(new Expense { Date = DateTime.Today, Amount = 500m, CategoryId = cat.Id });

        Assert.Equal(-400m, mgr.CalculateNetProfit());
    }

    // ── FM-33 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-33: GenerateReport — правильні суми та склади записів")]
    public void GenerateReport_CorrectTotalsAndRecords()
    {
        var mgr = BuildSampleManager();
        var report = mgr.GenerateReport(new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

        Assert.Equal(1500m, report.TotalIncome);
        Assert.Equal(200m,  report.TotalExpense);
        Assert.Equal(1300m, report.NetProfit);
        Assert.Equal(2, report.Incomes.Count);
        Assert.Single(report.Expenses);
    }

    // ── FM-34 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-34: GenerateReport — порожній діапазон → нулі")]
    public void GenerateReport_EmptyPeriod_ZeroTotals()
    {
        var mgr = BuildSampleManager();
        var report = mgr.GenerateReport(new DateTime(2020, 1, 1), new DateTime(2020, 1, 31));

        Assert.Equal(0m, report.TotalIncome);
        Assert.Equal(0m, report.TotalExpense);
        Assert.Empty(report.Incomes);
        Assert.Empty(report.Expenses);
    }
}

/// <summary>
/// Тест-кейс FM-F: Профілі та завантаження даних.
/// </summary>
public class FinanceManager_ProfileTests
{
    // ── FM-35 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-35: AddProfile — перший профіль стає активним")]
    public void AddProfile_FirstProfile_SetsActiveId()
    {
        var mgr = new FinanceManager();
        var p = new Profile { Name = "Main" };
        mgr.AddProfile(p);

        Assert.Single(mgr.Profiles);
        Assert.Equal(p.Id, mgr.ActiveProfileId);
    }

    // ── FM-35b ───────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-35b: AddProfile — другий профіль не змінює ActiveProfileId")]
    public void AddProfile_SecondProfile_DoesNotChangeActiveId()
    {
        var mgr = new FinanceManager();
        var p1 = new Profile { Name = "First" };
        var p2 = new Profile { Name = "Second" };
        mgr.AddProfile(p1);
        mgr.AddProfile(p2);

        Assert.Equal(p1.Id, mgr.ActiveProfileId);
    }

    // ── FM-36 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-36: SwitchProfile — перемикає ActiveProfileId")]
    public void SwitchProfile_ValidId_SwitchesActive()
    {
        var mgr = new FinanceManager();
        var p1 = new Profile { Name = "A" };
        var p2 = new Profile { Name = "B" };
        mgr.AddProfile(p1);
        mgr.AddProfile(p2);

        var result = mgr.SwitchProfile(p2.Id);

        Assert.True(result);
        Assert.Equal(p2.Id, mgr.ActiveProfileId);
    }

    // ── FM-37 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-37: SwitchProfile — неіснуючий Id → false, ActiveId незмінний")]
    public void SwitchProfile_UnknownId_ReturnsFalse()
    {
        var mgr = new FinanceManager();
        var p = new Profile { Name = "Main" };
        mgr.AddProfile(p);

        var result = mgr.SwitchProfile("ghost-id");

        Assert.False(result);
        Assert.Equal(p.Id, mgr.ActiveProfileId);
    }

    // ── FM-38 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-38: SetProfiles — ініціалізує колекцію та ActiveProfileId")]
    public void SetProfiles_InitializesCollection()
    {
        var mgr = new FinanceManager();
        var profiles = new List<Profile>
        {
            new() { Id = "a", Name = "A" },
            new() { Id = "b", Name = "B" }
        };
        mgr.SetProfiles(profiles, "b");

        Assert.Equal(2, mgr.Profiles.Count);
        Assert.Equal("b", mgr.ActiveProfileId);
    }

    // ── FM-39 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-39: LoadData — замінює всі колекції новими")]
    public void LoadData_ReplacesAllCollections()
    {
        var mgr = new FinanceManager();
        mgr.AddIncome(new Income { Date = DateTime.Today, Amount = 1m, Source = "Old" });

        var newIncomes    = new List<Income>  { new() { Date = DateTime.Today, Amount = 500m, Source = "New" } };
        var newExpenses   = new List<Expense> { };
        var newCategories = new List<Category>{ new() { Name = "New Cat" } };
        var newEntrepreneur = new Entrepreneur { FullName = "New FOP" };

        mgr.LoadData(newIncomes, newExpenses, newCategories, newEntrepreneur);

        Assert.Single(mgr.Incomes);
        Assert.Equal("New", mgr.Incomes[0].Source);
        Assert.Equal("New FOP", mgr.Entrepreneur.FullName);
    }

    // ── FM-40 ────────────────────────────────────────────────────────
    [Fact(DisplayName = "FM-40: LoadData — null-списки замінюються порожніми колекціями")]
    public void LoadData_NullLists_ReplacedWithEmpty()
    {
        var mgr = new FinanceManager();
        mgr.LoadData(null!, null!, null!);

        Assert.NotNull(mgr.Incomes);
        Assert.NotNull(mgr.Expenses);
        Assert.NotNull(mgr.Categories);
        Assert.Empty(mgr.Incomes);
    }
}
